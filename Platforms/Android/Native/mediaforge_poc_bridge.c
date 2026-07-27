#include "mediaforge_poc_bridge.h"

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/audio_fifo.h>
#include <libavutil/channel_layout.h>
#include <libavutil/error.h>
#include <libavutil/mem.h>
#include <libavutil/opt.h>
#include <libavutil/samplefmt.h>
#include <libswresample/swresample.h>

#define MF_CANCELLED (-2)

static void mf_set_error(char *buffer, int capacity, const char *context, int code)
{
    char details[AV_ERROR_MAX_STRING_SIZE] = {0};
    if (buffer == NULL || capacity <= 0) {
        return;
    }

    if (code < 0) {
        av_strerror(code, details, sizeof(details));
        snprintf(buffer, (size_t)capacity, "%s: %s", context, details);
    } else {
        snprintf(buffer, (size_t)capacity, "%s", context);
    }
}

static int mf_is_cancelled(const volatile int *cancel_requested)
{
    return cancel_requested != NULL && *cancel_requested != 0;
}

static enum AVSampleFormat mf_choose_sample_format(const AVCodec *codec)
{
    const enum AVSampleFormat *formats = NULL;
    int result = avcodec_get_supported_config(
        NULL,
        codec,
        AV_CODEC_CONFIG_SAMPLE_FORMAT,
        0,
        (const void **)&formats,
        NULL);
    if (result < 0 || formats == NULL || formats[0] == AV_SAMPLE_FMT_NONE) {
        return AV_SAMPLE_FMT_FLTP;
    }

    return formats[0];
}

static int mf_choose_sample_rate(const AVCodec *codec, int source_rate)
{
    const int *rates = NULL;
    const int *rate;
    int best = 0;
    int result = avcodec_get_supported_config(
        NULL,
        codec,
        AV_CODEC_CONFIG_SAMPLE_RATE,
        0,
        (const void **)&rates,
        NULL);
    if (result < 0 || rates == NULL) {
        return source_rate > 0 ? source_rate : 44100;
    }

    for (rate = rates; *rate != 0; rate++) {
        if (*rate == source_rate) {
            return source_rate;
        }
        if (best == 0 || abs(*rate - source_rate) < abs(best - source_rate)) {
            best = *rate;
        }
    }

    return best > 0 ? best : 44100;
}

static int mf_encode_available(
    AVAudioFifo *fifo,
    AVCodecContext *encoder,
    AVFormatContext *output,
    AVStream *output_stream,
    int flush_all,
    int64_t *next_pts)
{
    int result = 0;
    const int frame_size = encoder->frame_size > 0 ? encoder->frame_size : 1024;

    while (av_audio_fifo_size(fifo) >= frame_size ||
           (flush_all && av_audio_fifo_size(fifo) > 0)) {
        AVFrame *frame = av_frame_alloc();
        AVPacket *packet = av_packet_alloc();
        int samples;
        if (frame == NULL || packet == NULL) {
            av_frame_free(&frame);
            av_packet_free(&packet);
            return AVERROR(ENOMEM);
        }

        samples = av_audio_fifo_size(fifo) >= frame_size
            ? frame_size
            : av_audio_fifo_size(fifo);
        frame->nb_samples = samples;
        frame->format = encoder->sample_fmt;
        frame->sample_rate = encoder->sample_rate;
        result = av_channel_layout_copy(&frame->ch_layout, &encoder->ch_layout);
        if (result < 0) {
            av_frame_free(&frame);
            av_packet_free(&packet);
            return result;
        }

        result = av_frame_get_buffer(frame, 0);
        if (result < 0) {
            av_frame_free(&frame);
            av_packet_free(&packet);
            return result;
        }

        if (av_audio_fifo_read(fifo, (void **)frame->data, samples) < samples) {
            av_frame_free(&frame);
            av_packet_free(&packet);
            return AVERROR(EIO);
        }

        frame->pts = *next_pts;
        *next_pts += samples;
        result = avcodec_send_frame(encoder, frame);
        av_frame_free(&frame);
        if (result < 0) {
            av_packet_free(&packet);
            return result;
        }

        while ((result = avcodec_receive_packet(encoder, packet)) >= 0) {
            av_packet_rescale_ts(packet, encoder->time_base, output_stream->time_base);
            packet->stream_index = output_stream->index;
            result = av_interleaved_write_frame(output, packet);
            av_packet_unref(packet);
            if (result < 0) {
                av_packet_free(&packet);
                return result;
            }
        }

        av_packet_free(&packet);
        if (result != AVERROR(EAGAIN) && result != AVERROR_EOF) {
            return result;
        }
    }

    return 0;
}

static int mf_convert_frame(
    const AVFrame *decoded,
    SwrContext *resampler,
    AVCodecContext *encoder,
    AVAudioFifo *fifo)
{
    uint8_t **converted = NULL;
    int destination_samples;
    int converted_samples;
    int result;

    destination_samples = av_rescale_rnd(
        swr_get_delay(resampler, decoded->sample_rate) + decoded->nb_samples,
        encoder->sample_rate,
        decoded->sample_rate,
        AV_ROUND_UP);
    result = av_samples_alloc_array_and_samples(
        &converted,
        NULL,
        encoder->ch_layout.nb_channels,
        destination_samples,
        encoder->sample_fmt,
        0);
    if (result < 0) {
        return result;
    }

    converted_samples = swr_convert(
        resampler,
        converted,
        destination_samples,
        (const uint8_t **)decoded->extended_data,
        decoded->nb_samples);
    if (converted_samples < 0) {
        result = converted_samples;
        goto cleanup;
    }

    result = av_audio_fifo_realloc(
        fifo,
        av_audio_fifo_size(fifo) + converted_samples);
    if (result < 0) {
        goto cleanup;
    }

    if (av_audio_fifo_write(fifo, (void **)converted, converted_samples) <
        converted_samples) {
        result = AVERROR(EIO);
        goto cleanup;
    }

    result = 0;

cleanup:
    if (converted != NULL) {
        av_freep(&converted[0]);
    }
    av_freep(&converted);
    return result;
}

__attribute__((visibility("default")))
int mf_transcode_wav_to_m4a(
    const char *input_path,
    const char *output_path,
    mf_progress_callback progress,
    const volatile int *cancel_requested,
    char *error_message,
    int error_capacity)
{
    AVFormatContext *input = NULL;
    AVFormatContext *output = NULL;
    AVCodecContext *decoder = NULL;
    AVCodecContext *encoder = NULL;
    SwrContext *resampler = NULL;
    AVAudioFifo *fifo = NULL;
    AVPacket *packet = NULL;
    AVFrame *decoded = NULL;
    AVStream *input_stream = NULL;
    AVStream *output_stream = NULL;
    const AVCodec *decoder_codec;
    const AVCodec *encoder_codec;
    int audio_stream_index;
    int result = 0;
    int header_written = 0;
    int64_t next_pts = 0;
    int64_t duration_us = 0;

    if (input_path == NULL || output_path == NULL) {
        mf_set_error(error_message, error_capacity, "Input and output paths are required", 0);
        return AVERROR(EINVAL);
    }

    result = avformat_open_input(&input, input_path, NULL, NULL);
    if (result < 0) {
        mf_set_error(error_message, error_capacity, "Cannot open WAV input", result);
        goto cleanup;
    }

    result = avformat_find_stream_info(input, NULL);
    if (result < 0) {
        mf_set_error(error_message, error_capacity, "Cannot read WAV stream information", result);
        goto cleanup;
    }

    audio_stream_index = av_find_best_stream(
        input,
        AVMEDIA_TYPE_AUDIO,
        -1,
        -1,
        &decoder_codec,
        0);
    if (audio_stream_index < 0) {
        result = audio_stream_index;
        mf_set_error(error_message, error_capacity, "No readable WAV audio stream found", result);
        goto cleanup;
    }

    input_stream = input->streams[audio_stream_index];
    duration_us = input->duration > 0 ? input->duration : 0;
    decoder = avcodec_alloc_context3(decoder_codec);
    if (decoder == NULL) {
        result = AVERROR(ENOMEM);
        goto cleanup;
    }

    result = avcodec_parameters_to_context(decoder, input_stream->codecpar);
    if (result < 0 || (result = avcodec_open2(decoder, decoder_codec, NULL)) < 0) {
        mf_set_error(error_message, error_capacity, "Cannot open WAV decoder", result);
        goto cleanup;
    }

    encoder_codec = avcodec_find_encoder(AV_CODEC_ID_AAC);
    if (encoder_codec == NULL) {
        result = AVERROR_ENCODER_NOT_FOUND;
        mf_set_error(error_message, error_capacity, "AAC encoder is unavailable", result);
        goto cleanup;
    }

    result = avformat_alloc_output_context2(&output, NULL, "mov", output_path);
    if (result < 0 || output == NULL) {
        mf_set_error(error_message, error_capacity, "Cannot create M4A container", result);
        goto cleanup;
    }

    output_stream = avformat_new_stream(output, NULL);
    encoder = avcodec_alloc_context3(encoder_codec);
    if (output_stream == NULL || encoder == NULL) {
        result = AVERROR(ENOMEM);
        goto cleanup;
    }

    encoder->sample_fmt = mf_choose_sample_format(encoder_codec);
    encoder->sample_rate = mf_choose_sample_rate(encoder_codec, decoder->sample_rate);
    encoder->bit_rate = 128000;
    encoder->time_base = (AVRational){1, encoder->sample_rate};
    av_channel_layout_default(
        &encoder->ch_layout,
        decoder->ch_layout.nb_channels == 1 ? 1 : 2);

    if ((output->oformat->flags & AVFMT_GLOBALHEADER) != 0) {
        encoder->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;
    }

    result = avcodec_open2(encoder, encoder_codec, NULL);
    if (result < 0) {
        char context[160];
        snprintf(
            context,
            sizeof(context),
            "Cannot open AAC encoder (rate=%d, channels=%d, sample-format=%d)",
            encoder->sample_rate,
            encoder->ch_layout.nb_channels,
            encoder->sample_fmt);
        mf_set_error(error_message, error_capacity, context, result);
        goto cleanup;
    }

    output_stream->time_base = encoder->time_base;
    result = avcodec_parameters_from_context(output_stream->codecpar, encoder);
    if (result < 0) {
        goto cleanup;
    }

    result = swr_alloc_set_opts2(
        &resampler,
        &encoder->ch_layout,
        encoder->sample_fmt,
        encoder->sample_rate,
        &decoder->ch_layout,
        decoder->sample_fmt,
        decoder->sample_rate,
        0,
        NULL);
    if (result < 0 || (result = swr_init(resampler)) < 0) {
        mf_set_error(error_message, error_capacity, "Cannot initialize audio resampling", result);
        goto cleanup;
    }

    fifo = av_audio_fifo_alloc(
        encoder->sample_fmt,
        encoder->ch_layout.nb_channels,
        encoder->frame_size > 0 ? encoder->frame_size : 1024);
    packet = av_packet_alloc();
    decoded = av_frame_alloc();
    if (fifo == NULL || packet == NULL || decoded == NULL) {
        result = AVERROR(ENOMEM);
        goto cleanup;
    }

    if ((output->oformat->flags & AVFMT_NOFILE) == 0) {
        result = avio_open(&output->pb, output_path, AVIO_FLAG_WRITE);
        if (result < 0) {
            mf_set_error(error_message, error_capacity, "Cannot open temporary M4A output", result);
            goto cleanup;
        }
    }

    result = avformat_write_header(output, NULL);
    if (result < 0) {
        mf_set_error(error_message, error_capacity, "Cannot write M4A header", result);
        goto cleanup;
    }
    header_written = 1;

    while ((result = av_read_frame(input, packet)) >= 0) {
        if (mf_is_cancelled(cancel_requested)) {
            result = MF_CANCELLED;
            goto cleanup;
        }
        if (packet->stream_index != audio_stream_index) {
            av_packet_unref(packet);
            continue;
        }

        result = avcodec_send_packet(decoder, packet);
        av_packet_unref(packet);
        if (result < 0) {
            goto cleanup;
        }

        while ((result = avcodec_receive_frame(decoder, decoded)) >= 0) {
            result = mf_convert_frame(decoded, resampler, encoder, fifo);
            av_frame_unref(decoded);
            if (result < 0) {
                goto cleanup;
            }
            result = mf_encode_available(
                fifo,
                encoder,
                output,
                output_stream,
                0,
                &next_pts);
            if (result < 0) {
                goto cleanup;
            }

            if (progress != NULL && duration_us > 0) {
                int64_t processed_us = av_rescale_q(
                    next_pts,
                    encoder->time_base,
                    AV_TIME_BASE_Q);
                double fraction = (double)processed_us / (double)duration_us;
                progress(
                    fraction > 0.99 ? 0.99 : fraction,
                    processed_us / 1000);
            }
        }

        if (result != AVERROR(EAGAIN) && result != AVERROR_EOF) {
            goto cleanup;
        }
    }

    if (result != AVERROR_EOF) {
        goto cleanup;
    }

    result = avcodec_send_packet(decoder, NULL);
    if (result < 0) {
        goto cleanup;
    }
    while ((result = avcodec_receive_frame(decoder, decoded)) >= 0) {
        result = mf_convert_frame(decoded, resampler, encoder, fifo);
        av_frame_unref(decoded);
        if (result < 0) {
            goto cleanup;
        }
    }
    if (result != AVERROR_EOF && result != AVERROR(EAGAIN)) {
        goto cleanup;
    }

    result = mf_encode_available(
        fifo,
        encoder,
        output,
        output_stream,
        1,
        &next_pts);
    if (result < 0) {
        goto cleanup;
    }

    result = avcodec_send_frame(encoder, NULL);
    if (result < 0) {
        goto cleanup;
    }
    while ((result = avcodec_receive_packet(encoder, packet)) >= 0) {
        av_packet_rescale_ts(packet, encoder->time_base, output_stream->time_base);
        packet->stream_index = output_stream->index;
        result = av_interleaved_write_frame(output, packet);
        av_packet_unref(packet);
        if (result < 0) {
            goto cleanup;
        }
    }
    if (result != AVERROR_EOF && result != AVERROR(EAGAIN)) {
        goto cleanup;
    }

    result = av_write_trailer(output);
    if (result < 0) {
        mf_set_error(error_message, error_capacity, "Cannot finalize M4A container", result);
        goto cleanup;
    }
    header_written = 0;
    if (progress != NULL) {
        progress(1.0, av_rescale_q(next_pts, encoder->time_base, AV_TIME_BASE_Q) / 1000);
    }
    result = 0;

cleanup:
    if (result < 0 && result != MF_CANCELLED && error_message != NULL &&
        error_message[0] == '\0') {
        mf_set_error(error_message, error_capacity, "WAV to M4A conversion failed", result);
    }
    if (header_written && output != NULL) {
        av_write_trailer(output);
    }
    if (output != NULL && output->pb != NULL) {
        avio_closep(&output->pb);
    }
    av_frame_free(&decoded);
    av_packet_free(&packet);
    av_audio_fifo_free(fifo);
    swr_free(&resampler);
    avcodec_free_context(&encoder);
    avcodec_free_context(&decoder);
    avformat_free_context(output);
    avformat_close_input(&input);
    return result;
}

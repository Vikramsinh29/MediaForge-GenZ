#ifndef MEDIAFORGE_POC_BRIDGE_H
#define MEDIAFORGE_POC_BRIDGE_H

#include <stddef.h>

#if defined(__cplusplus)
extern "C" {
#endif

typedef void (*mf_progress_callback)(double fraction, long long processed_milliseconds);

int mf_transcode_wav_to_m4a(
    const char *input_path,
    const char *output_path,
    mf_progress_callback progress,
    const volatile int *cancel_requested,
    char *error_message,
    int error_capacity);

#if defined(__cplusplus)
}
#endif

#endif

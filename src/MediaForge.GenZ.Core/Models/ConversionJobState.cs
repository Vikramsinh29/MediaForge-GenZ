namespace MediaForge.GenZ.Core.Models;

public enum ConversionJobState
{
    Queued,
    Preparing,
    Processing,
    Completed,
    Failed,
    Cancelled
}

namespace DownKyi.Application.Desktop;

public sealed record DynamicVideoNavigationPayload
{
    public DynamicVideoNavigationPayload(long mid)
    {
        if (mid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mid), mid, "The uploader MID must be positive.");
        }

        Mid = mid;
    }

    public long Mid { get; }
}

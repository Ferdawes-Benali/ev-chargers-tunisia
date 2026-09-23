namespace EvChargers.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Data, int Page, int Size, int Total)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)Size);
}
namespace Elfive.Core.RLL;

public static class LayoutCalculator
{
    public static LayoutSize Measure(IRungElement element)
    {
        return element switch
        {
            Instruction => new LayoutSize { Width = 1, Height = 1 },
            Series s => new LayoutSize
            {
                Width = s.Elements.Sum(e => Measure(e).Width),
                Height = s.Elements.Count == 0 ? 1 : s.Elements.Max(e => Measure(e).Height),
            },
            Parallel p => new LayoutSize
            {
                Width = p.Branches.Count == 0 ? 1 : p.Branches.Max(e => Measure(e).Width) + 1,
                Height = p.Branches.Sum(e => Measure(e).Height),
            },
            _ => new LayoutSize { Width = 0, Height = 0}
        };
    }
}

public struct LayoutSize
{
    public int Width { get; set; }
    public int Height { get; set; }
}
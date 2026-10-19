namespace Sprout.Core;

public static class TaskSizeExtensions
{
    public static double Hours(this TaskSize size) => size switch
    {
        TaskSize.XS => 0.5,
        TaskSize.S => 1,
        TaskSize.M => 2.5,
        TaskSize.L => 4,
        TaskSize.XL => 6,
        _ => 1,
    };
}


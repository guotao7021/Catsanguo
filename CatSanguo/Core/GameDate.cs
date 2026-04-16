using System;

namespace CatSanguo.Core;

/// <summary>
/// 游戏内日期结构体，支持年/月/日的管理以及日期推进。
/// 采用"旬"制：每月3旬，每旬10天，便于回合制推进。
/// </summary>
public struct GameDate : IEquatable<GameDate>
{
    /// <summary>年份（公元纪年）</summary>
    public int Year { get; private set; }

    /// <summary>月份（1-12）</summary>
    public int Month { get; private set; }

    /// <summary>旬（1-3，每旬10天）</summary>
    public int Xun { get; private set; }

    /// <summary>旬内天数（1-10）</summary>
    public int DayInXun { get; private set; }

    /// <summary>总天数（从基准日期起算，用于内部计算）</summary>
    public int TotalDays { get; private set; }

    /// <summary>是否月末（旬末最后一天）</summary>
    public bool IsMonthEnd => Xun == 3 && DayInXun == 10;

    /// <summary>是否旬末</summary>
    public bool IsXunEnd => DayInXun == 10;

    /// <summary>是否季末（每季末月：3/6/9/12的月末）</summary>
    public bool IsQuarterEnd => (Month % 3 == 0) && IsMonthEnd;

    /// <summary>是否年末</summary>
    public bool IsYearEnd => Month == 12 && IsMonthEnd;

    /// <summary>基准年份</summary>
    private const int BaseYear = 184; // 东汉中平元年（黄巾之乱）

    /// <summary>
    /// 创建游戏日期
    /// </summary>
    /// <param name="year">年份</param>
    /// <param name="month">月份（1-12）</param>
    /// <param name="xun">旬（1-3）</param>
    /// <param name="dayInXun">旬内天数（1-10）</param>
    public GameDate(int year, int month, int xun, int dayInXun)
    {
        Year = year;
        Month = Clamp(month, 1, 12);
        Xun = Clamp(xun, 1, 3);
        DayInXun = Clamp(dayInXun, 1, 10);
        TotalDays = CalculateTotalDays();
    }

    /// <summary>
    /// 从总天数创建日期
    /// </summary>
    public static GameDate FromTotalDays(int totalDays)
    {
        int remainingDays = totalDays;

        // 计算年份（每年360天 = 12月 * 30天）
        int year = BaseYear + remainingDays / 360;
        remainingDays %= 360;
        if (remainingDays < 0)
        {
            year--;
            remainingDays += 360;
        }

        // 计算月份（每月30天）
        int month = remainingDays / 30 + 1;
        remainingDays %= 30;

        // 计算旬（每旬10天）
        int xun = remainingDays / 10 + 1;
        int dayInXun = remainingDays % 10 + 1;

        return new GameDate(year, month, xun, dayInXun);
    }

    /// <summary>
    /// 从字符串解析日期，格式："年-月-旬-日"，如"184-1-1-1"
    /// </summary>
    public static GameDate Parse(string dateStr)
    {
        var parts = dateStr.Split('-');
        if (parts.Length == 4 &&
            int.TryParse(parts[0], out int year) &&
            int.TryParse(parts[1], out int month) &&
            int.TryParse(parts[2], out int xun) &&
            int.TryParse(parts[3], out int day))
        {
            return new GameDate(year, month, xun, day);
        }
        throw new FormatException($"Invalid GameDate format: {dateStr}. Expected 'year-month-xun-day'");
    }

    /// <summary>
    /// 尝试解析日期
    /// </summary>
    public static bool TryParse(string dateStr, out GameDate result)
    {
        try
        {
            result = Parse(dateStr);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// 推进指定天数
    /// </summary>
    public void AddDays(int days)
    {
        TotalDays += days;
        var newDate = FromTotalDays(TotalDays);
        Year = newDate.Year;
        Month = newDate.Month;
        Xun = newDate.Xun;
        DayInXun = newDate.DayInXun;
    }

    /// <summary>
    /// 推进一旬（10天）
    /// </summary>
    public void AddXun()
    {
        AddDays(10);
    }

    /// <summary>
    /// 获取月份中的天（1-30）
    /// </summary>
    public int DayOfMonth => (Xun - 1) * 10 + DayInXun;

    /// <summary>
    /// 获取季节（1-4）
    /// </summary>
    public int Season => (Month - 1) / 3 + 1;

    /// <summary>
    /// 获取季节名称
    /// </summary>
    public string SeasonName => Season switch
    {
        1 => "春",
        2 => "夏",
        3 => "秋",
        4 => "冬",
        _ => "未知"
    };

    /// <summary>
    /// 转换为显示字符串，如"中平元年 正月 上旬 一日"
    /// </summary>
    public string ToDisplayString()
    {
        string yearName = GetYearName(Year);
        string monthName = GetMonthName(Month);
        string xunName = Xun switch
        {
            1 => "上旬",
            2 => "中旬",
            3 => "下旬",
            _ => ""
        };
        string dayName = GetDayName(DayInXun);

        return $"{yearName} {monthName} {xunName} {dayName}";
    }

    /// <summary>
    /// 转换为简洁显示字符串，如"184-1-1-1"
    /// </summary>
    public string ToShortString()
    {
        return $"{Year}-{Month}-{Xun}-{DayInXun}";
    }

    /// <summary>
    /// 计算从基准日期起算的总天数
    /// </summary>
    private int CalculateTotalDays()
    {
        int years = Year - BaseYear;
        return years * 360 + (Month - 1) * 30 + (Xun - 1) * 10 + (DayInXun - 1);
    }

    /// <summary>
    /// 获取年份名称（年号）
    /// </summary>
    private static string GetYearName(int year)
    {
        // 简化的年号系统
        if (year < 189) return $"中平{year - 184 + 1}年";
        if (year < 194) return $"初平{year - 189 + 1}年";
        if (year < 200) return $"建安{year - 194 + 1}年";
        if (year < 220) return $"延康{year - 220 + 1}年";
        return $"{year}年";
    }

    /// <summary>
    /// 获取月份名称（农历）
    /// </summary>
    private static string GetMonthName(int month)
    {
        return month switch
        {
            1 => "正月",
            2 => "二月",
            3 => "三月",
            4 => "四月",
            5 => "五月",
            6 => "六月",
            7 => "七月",
            8 => "八月",
            9 => "九月",
            10 => "十月",
            11 => "十一月",
            12 => "腊月",
            _ => ""
        };
    }

    /// <summary>
    /// 获取天数名称
    /// </summary>
    private static string GetDayName(int day)
    {
        return day switch
        {
            1 => "一日",
            2 => "二日",
            3 => "三日",
            4 => "四日",
            5 => "五日",
            6 => "六日",
            7 => "七日",
            8 => "八日",
            9 => "九日",
            10 => "十日",
            _ => ""
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public bool Equals(GameDate other)
    {
        return TotalDays == other.TotalDays;
    }

    public override bool Equals(object? obj)
    {
        return obj is GameDate other && Equals(other);
    }

    public override int GetHashCode()
    {
        return TotalDays.GetHashCode();
    }

    public static bool operator ==(GameDate left, GameDate right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GameDate left, GameDate right)
    {
        return !left.Equals(right);
    }

    public static bool operator <(GameDate left, GameDate right)
    {
        return left.TotalDays < right.TotalDays;
    }

    public static bool operator >(GameDate left, GameDate right)
    {
        return left.TotalDays > right.TotalDays;
    }

    public static bool operator <=(GameDate left, GameDate right)
    {
        return left.TotalDays <= right.TotalDays;
    }

    public static bool operator >=(GameDate left, GameDate right)
    {
        return left.TotalDays >= right.TotalDays;
    }
}

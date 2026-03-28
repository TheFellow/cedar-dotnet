using Cedar.Types;

namespace Cedar.Ast;

public static class ExtensionOperators
{
    public static Node LessThanDecimal(this Node lhs, Node rhs)
    {
        return Operators.ExtensionCall("lessThan", lhs, rhs);
    }

    public static Node LessThanOrEqualDecimal(this Node lhs, Node rhs)
    {
        return Operators.ExtensionCall("lessThanOrEqual", lhs, rhs);
    }

    public static Node GreaterThanDecimal(this Node lhs, Node rhs)
    {
        return Operators.ExtensionCall("greaterThan", lhs, rhs);
    }

    public static Node GreaterThanOrEqualDecimal(this Node lhs, Node rhs)
    {
        return Operators.ExtensionCall("greaterThanOrEqual", lhs, rhs);
    }

    public static Node IsIpv4(this Node lhs)
    {
        return Operators.ExtensionCall("isIpv4", lhs);
    }

    public static Node IsIpv6(this Node lhs)
    {
        return Operators.ExtensionCall("isIpv6", lhs);
    }

    public static Node IsLoopback(this Node lhs)
    {
        return Operators.ExtensionCall("isLoopback", lhs);
    }

    public static Node IsMulticast(this Node lhs)
    {
        return Operators.ExtensionCall("isMulticast", lhs);
    }

    public static Node IsInRange(this Node lhs, Node rhs)
    {
        return Operators.ExtensionCall("isInRange", lhs, rhs);
    }

    public static Node Offset(this Node lhs, Node rhs)
    {
        return Operators.ExtensionCall("offset", lhs, rhs);
    }

    public static Node DurationSince(this Node lhs, Node rhs)
    {
        return Operators.ExtensionCall("durationSince", lhs, rhs);
    }

    public static Node DaysInMonth(this Node lhs)
    {
        return Operators.ExtensionCall("daysInMonth", lhs);
    }

    public static Node Year(this Node lhs)
    {
        return Operators.ExtensionCall("year", lhs);
    }

    public static Node Month(this Node lhs)
    {
        return Operators.ExtensionCall("month", lhs);
    }

    public static Node Day(this Node lhs)
    {
        return Operators.ExtensionCall("day", lhs);
    }

    public static Node DayOfWeek(this Node lhs)
    {
        return Operators.ExtensionCall("dayOfWeek", lhs);
    }

    public static Node DayOfYear(this Node lhs)
    {
        return Operators.ExtensionCall("dayOfYear", lhs);
    }

    public static Node Hour(this Node lhs)
    {
        return Operators.ExtensionCall("hour", lhs);
    }

    public static Node Minute(this Node lhs)
    {
        return Operators.ExtensionCall("minute", lhs);
    }

    public static Node Second(this Node lhs)
    {
        return Operators.ExtensionCall("second", lhs);
    }

    public static Node Millisecond(this Node lhs)
    {
        return Operators.ExtensionCall("millisecond", lhs);
    }

    public static Node ToDate(this Node lhs)
    {
        return Operators.ExtensionCall("toDate", lhs);
    }

    public static Node ToTime(this Node lhs)
    {
        return Operators.ExtensionCall("toTime", lhs);
    }

    public static Node ToDays(this Node lhs)
    {
        return Operators.ExtensionCall("toDays", lhs);
    }

    public static Node ToHours(this Node lhs)
    {
        return Operators.ExtensionCall("toHours", lhs);
    }

    public static Node ToMinutes(this Node lhs)
    {
        return Operators.ExtensionCall("toMinutes", lhs);
    }

    public static Node ToSeconds(this Node lhs)
    {
        return Operators.ExtensionCall("toSeconds", lhs);
    }

    public static Node ToMilliseconds(this Node lhs)
    {
        return Operators.ExtensionCall("toMilliseconds", lhs);
    }

    public static Node Decimal(Node rhs)
    {
        return Operators.ExtensionCall("decimal", rhs);
    }

    public static Node Ip(Node rhs)
    {
        return Operators.ExtensionCall("ip", rhs);
    }

    public static Node Datetime(Node rhs)
    {
        return Operators.ExtensionCall("datetime", rhs);
    }

    public static Node Duration(Node rhs)
    {
        return Operators.ExtensionCall("duration", rhs);
    }
}

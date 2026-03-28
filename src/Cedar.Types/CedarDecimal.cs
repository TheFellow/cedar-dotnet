using System;
using System.Globalization;

namespace Cedar.Types;

public sealed record CedarDecimal : CedarValue, IComparable<CedarDecimal>
{
    private const long Precision = 10_000;
    private const long MaxIntegerPart = 922_337_203_685_477;
    private const short MaxFractionalPart = 5_807;
    private const short MinFractionalPart = -5_808;

    private CedarDecimal(long value)
    {
        Value = value;
    }

    private long Value { get; }

    public static CedarDecimal DecimalMax { get; } = new(long.MaxValue);
    public static CedarDecimal DecimalMin { get; } = new(long.MinValue);

    public static CedarDecimal NewDecimal(long value, int exponent)
    {
        if (exponent is < -4 or > 14)
        {
            throw new ArgumentOutOfRangeException(nameof(exponent), "Decimal exponent must be between -4 and 14.");
        }

        long integerPart;
        long fractionalPart;

        if (exponent <= 0)
        {
            long divisor = Pow10(-exponent);
            integerPart = value / divisor;
            fractionalPart = (value % divisor) * Pow10(4 + exponent);
        }
        else
        {
            try
            {
                integerPart = checked(value * Pow10(exponent));
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Decimal value is out of range.");
            }

            fractionalPart = 0;
        }

        return FromParts(integerPart, checked((short)fractionalPart));
    }

    public static CedarDecimal NewDecimalFromInt(long value)
    {
        return NewDecimal(value, 0);
    }

    public static CedarDecimal NewDecimalFromFloat(double value)
    {
        double scaled = value * Precision;
        if (scaled > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Decimal value would overflow.");
        }

        if (scaled < long.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Decimal value would underflow.");
        }

        return NewDecimal((long)scaled, -4);
    }

    public static CedarDecimal Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int decimalIndex = value.IndexOf('.');
        if (decimalIndex < 0)
        {
            throw new FormatException("Decimal values must contain a decimal point.");
        }

        if (!long.TryParse(value[..decimalIndex], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integerPart))
        {
            throw new FormatException("The decimal integer component is invalid.");
        }

        string fractionalText = value[(decimalIndex + 1)..];
        if (fractionalText.Length is 0 or > 4)
        {
            throw new FormatException("Decimal values must contain between 1 and 4 fractional digits.");
        }

        if (!ushort.TryParse(fractionalText, NumberStyles.None, CultureInfo.InvariantCulture, out ushort fractionalDigits))
        {
            throw new FormatException("The decimal fractional component is invalid.");
        }

        short fractionalPart = checked((short)(fractionalDigits * Pow10(4 - fractionalText.Length)));
        if (value[0] == '-')
        {
            fractionalPart = checked((short)-fractionalPart);
        }

        return FromParts(integerPart, fractionalPart);
    }

    public double ToDouble()
    {
        return Value / (double)Precision;
    }

    public int CompareTo(CedarDecimal? other)
    {
        return other is null ? 1 : Value.CompareTo(other.Value);
    }

    public override string MarshalCedar()
    {
        return "decimal(\"" + FormatValue() + "\")";
    }

    public bool Equals(CedarDecimal? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && Value == other.Value;
    }

    public override int GetHashCode()
    {
        return CedarHash.ForInt64(nameof(CedarDecimal), Value);
    }

    private static CedarDecimal FromParts(long integerPart, short fractionalPart)
    {
        if (integerPart > MaxIntegerPart || (integerPart == MaxIntegerPart && fractionalPart > MaxFractionalPart))
        {
            throw new ArgumentOutOfRangeException(nameof(integerPart), "Decimal value is out of range.");
        }

        if (integerPart < -MaxIntegerPart || (integerPart == -MaxIntegerPart && fractionalPart < MinFractionalPart))
        {
            throw new ArgumentOutOfRangeException(nameof(integerPart), "Decimal value is out of range.");
        }

        long raw = checked((integerPart * Precision) + fractionalPart);
        return new CedarDecimal(raw);
    }

    private static long Pow10(int exponent)
    {
        long result = 1;
        for (int index = 0; index < exponent; index++)
        {
            result *= 10;
        }

        return result;
    }

    private string FormatValue()
    {
        string result;

        if (Value < 0)
        {
            long integer = Value / Precision;
            long fractional = (integer * Precision) - Value;
            result = string.Create(CultureInfo.InvariantCulture, $"-{-integer}.{fractional:0000}");
        }
        else
        {
            result = string.Create(CultureInfo.InvariantCulture, $"{Value / Precision}.{Value % Precision:0000}");
        }

        int right = result.Length;
        int trimmed = 0;

        while (right > 0 && trimmed < 3 && result[right - 1] == '0')
        {
            right--;
            trimmed++;
        }

        return result[..right];
    }
}

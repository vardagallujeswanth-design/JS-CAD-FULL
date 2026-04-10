using System.Text;
using System.Text.RegularExpressions;

namespace CadProcessorService.Helpers;

public static class ValidationHelper
{
    public static string RemoveNonDigits(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            if (c >= '0' && c <= '9')
                sb.Append(c);
        }
        return sb.ToString();
    }

    public static bool HasSpecialChar(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        const string specialChar = @"\|!#$%&/()=?»«@£§€{}.;'<>_,";
        foreach (var ch in specialChar)
        {
            if (input.Contains(ch)) return true;
            }
        return false;
    }
}

namespace DynamicBanner.Utils
{
    internal static class MathExtensions
    {
        public static int GreatestCommonDivisor(this int number, int other)
        {
            while( other != 0 )
            {
                var remainder = number % other;
                number = other;
                other = remainder;
            }

            return number;
        }
    }
}
namespace SkillScale
{
    internal static class Utilities
    {
        /// <summary>
        /// 50 adds 50%, -50 subtracts 50%, -100 zeroes the value.
        /// </summary>
        internal static float ApplyModifierValue(float targetValue, float value)
        {
            if (value <= -100f)
            {
                value = -100f;
            }

            if (value >= 0f)
            {
                return targetValue + targetValue / 100f * value;
            }

            return targetValue - targetValue / 100f * -value;
        }
    }
}

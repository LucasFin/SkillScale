namespace SkillScale
{
    internal static class Utilities
    {
        internal static float Scale(float value, float multiplier)
        {
            if (multiplier < 0f)
            {
                multiplier = 0f;
            }

            return value * multiplier;
        }
    }
}

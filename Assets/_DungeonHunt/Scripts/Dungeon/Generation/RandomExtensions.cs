using System.Collections.Generic;

public static class RandomExtensions
{
    public static T Pick<T>(this System.Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    public static void Shuffle<T>(this System.Random rng, IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}

namespace Sundstrom.CheckedExceptions;

public static class HashSetExtensions
{
    /// <summary>
    /// Adds the elements of the specified collection to the current HashSet.
    /// </summary>
    /// <typeparam name="T">The type of elements in the HashSet.</typeparam>
    /// <param name="set">The HashSet to add elements to.</param>
    /// <param name="items">The collection of items to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if the set or items are null.</exception>
    public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items)
    {
        if (set is null)
            throw new ArgumentNullException(nameof(set));
        if (items is null)
            throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            set.Add(item); // HashSet.Add ensures uniqueness
        }
    }
}
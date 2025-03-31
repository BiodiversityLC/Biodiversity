namespace Biodiversity.Util.DataStructures;

//There are no mutable tuples in C# for some reason.
public class Pair<T1, T2>
{
    public Pair(T1 first, T2 second)
    {
        First = first;
        Second = second;
    }
    public T1 First { get; set; }
    public T2 Second { get; set; }
}
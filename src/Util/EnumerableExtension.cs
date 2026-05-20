using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kanban.Util
{
    public static class EnumerableExtension
    {
        /// <summary>
        /// Usage:
        /// 1. For one property var query = people.DistinctBy(p => p.Id);
        /// 2. For multiple properties, you can use anonymous types, which implement equality appropriately:
        ///    var query = people.DistinctBy(p => new { p.Id, p.Name });
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// 連続項目をグループ化する。Funcにて同じグループ化を判定する。
        /// Groups items based on a function that takes the current and previous items
        /// to determin if that item should be in the existing group, or a new group.
        /// Sample usage:
        /// var intervals = dates.GroupWhile((prev, next) => 
        ///      prev.Date.AddDays(1) == next.Date)
        ///   .Select(g => new sampleWithIntervals()
        ///    {
        ///      startDate = g.Min(s => s.Date),
        ///      endDate = g.Max(s => s.Date)
        ///    });
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">source collection</param>
        /// <param name="predicate">function which determin whether two elements are same group</param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<T>> GroupContinuous<T>(this IEnumerable<T> source, Func<T, T, bool> predicate)
        {
            using (var iterator = source.GetEnumerator())
            {
                if (!iterator.MoveNext())
                    yield break;

                List<T> list = new List<T>() { iterator.Current };

                T previous = iterator.Current;

                while (iterator.MoveNext())
                {
                    if (predicate(previous, iterator.Current))
                    {
                        list.Add(iterator.Current);
                    }
                    else
                    {
                        yield return list;
                        list = new List<T>() { iterator.Current };
                    }

                    previous = iterator.Current;
                }
                yield return list;
            }
        }
    }
}

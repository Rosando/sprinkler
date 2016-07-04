using System.Collections.Generic;
using System.Linq;

namespace Furore.Fhir.Sprinkler.XunitRunner.FhirExtensions
{
    internal static class MyLinqExtensions
    {
        public static IEnumerable<T[]> BatchArray<T>(
            this IEnumerable<T> source, int batchSize)
        {
            return Batch<T>(source, batchSize).Select(x => x.ToArray());
        }

        public static IEnumerable<IEnumerable<T>> Batch<T>(
            this IEnumerable<T> source, int batchSize)
        {
            using (var enumerator = source.GetEnumerator())
                while (enumerator.MoveNext())
                    yield return YieldBatchElements(enumerator, batchSize);
        }

        private static IEnumerable<T> YieldBatchElements<T>(
            IEnumerator<T> source, int batchSize)
        {
            for (int i = 0; i < batchSize; i++)
            {
                yield return source.Current;
                if(i < batchSize && (source.MoveNext() == false))
                    break;

            }
        }
    }
}
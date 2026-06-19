using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace MemoFramework
{
    public partial class MFUtils
    {
        public static class Random
        {
            /// <summary>
            /// 生成[min,max]内的n个不重复随机数
            /// </summary>
            /// <param name="min">范围最小值(包含)</param>
            /// <param name="max">范围最大值(包含)</param>
            /// <param name="count">需要生成的随机数个数</param>
            /// <returns>包含不重复随机数的List</returns>
            /// <exception cref="System.ArgumentException">当请求的随机数个数大于可用范围时抛出</exception>
            public static List<int> NumsInRange(int min, int max, int count)
            {
                // 检查参数有效性
                int rangeSize = max - min + 1;
                if (count > rangeSize)
                {
                    throw new System.ArgumentException(
                        $"无法在范围:[{min},{max}]内生成{count}个随机数,范围内只有{rangeSize}个数字");
                }

                // 创建源数据List
                List<int> sourceNumbers = new List<int>();
                for (int i = min; i <= max; i++)
                {
                    sourceNumbers.Add(i);
                }

                // 创建结果List
                List<int> result = new List<int>();

                // 从源数据中随机选择数字
                while (result.Count < count)
                {
                    int randomIndex = UnityEngine.Random.Range(0, sourceNumbers.Count);
                    result.Add(sourceNumbers[randomIndex]);
                    sourceNumbers.RemoveAt(randomIndex);
                }

                return result;
            }

            public static int SelectIndexFromWeights(IEnumerable<int> weights)
            {
                List<int> list = weights == null ? null : weights.ToList();
                if (list == null || list.Count == 0)
                    throw new ArgumentException("权重列表不能为空。");
                int sum = 0;

                foreach (var weight in list)
                {
                    if (weight < 0)
                        throw new ArgumentException("权重必须为非负整数。");
                    sum += weight;
                }

                if (sum == 0)
                    throw new ArgumentException("总权重必须大于零。");

                var totalWeight = sum;
                int randomValue = UnityEngine.Random.Range(0, totalWeight + 1);
                int currentWeight = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    currentWeight += list[i];
                    if (randomValue <= currentWeight)
                        return i;
                }

                return -1;
            }

            public static int SelectIndexFromWeights(IEnumerable<float> weights)
            {
                List<float> list = weights == null ? null : weights.ToList();
                if (list == null || list.Count == 0)
                    throw new ArgumentException("权重列表不能为空。");

                float sum = 0;

                foreach (var weight in list)
                {
                    if (weight < 0)
                        throw new ArgumentException("权重必须为非负整数。");
                    sum += weight;
                }

                if (sum == 0)
                    throw new ArgumentException("总权重必须大于零。");

                var totalWeight = sum;
                float randomValue = UnityEngine.Random.Range(0, totalWeight);
                float currentWeight = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    currentWeight += list[i];
                    if (randomValue <= currentWeight)
                        return i;
                }

                return -1;
            }

            /// <summary>
            /// 从源List中随机选择指定数量的元素
            /// </summary>
            /// <param name="source">源数据List</param>
            /// <param name="count">需要选择的数量</param>
            /// <returns>包含随机选择元素的新List</returns>
            /// <exception cref="System.ArgumentException">当请求数量大于源List长度时抛出</exception>
            public static List<int> SelectIndicesFromWeights(List<int> source, int count)
            {
                if (count > source.Count)
                {
                    throw new System.ArgumentException(
                        $"无法从长度为{source.Count}的List中选择{count}个元素");
                }

                List<int> result = new List<int>();
                List<int> tempIndices = new List<int>();

                // 创建索引列表
                for (int i = 0; i < source.Count; i++)
                {
                    tempIndices.Add(i);
                }

                // 随机选择指定数量的索引
                for (int i = 0; i < count; i++)
                {
                    int randomIndex = UnityEngine.Random.Range(0, tempIndices.Count);
                    result.Add(source[tempIndices[randomIndex]]);
                    tempIndices.RemoveAt(randomIndex);
                }

                return result;
            }
        }
    }
}
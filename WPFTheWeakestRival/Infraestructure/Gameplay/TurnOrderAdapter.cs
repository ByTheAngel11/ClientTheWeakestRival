using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WPFTheWeakestRival.Infrastructure.Gameplay.Match
{
    internal sealed class TurnOrderSnapshot
    {
        public TurnOrderSnapshot(int currentTurnUserId, int[] orderedAliveUserIds)
        {
            CurrentTurnUserId = currentTurnUserId;
            OrderedAliveUserIds = orderedAliveUserIds ?? Array.Empty<int>();
        }

        public int CurrentTurnUserId { get; }

        public int[] OrderedAliveUserIds { get; }
    }

    internal static class TurnOrderAdapter
    {
        private static readonly string[] CurrentTurnUserIdPropertyNames =
        {
            "CurrentTurnUserId",
            "CurrentUserId",
            "TurnUserId",
            "CurrentPlayerUserId"
        };

        private static readonly string[] OrderedAliveUserIdsPropertyNames =
        {
            "OrderedAliveUserIds",
            "OrderedUserIds",
            "AliveUserIdsInOrder",
            "TurnOrderUserIds",
            "OrderUserIds"
        };

        public static TurnOrderSnapshot ToSnapshot(object dto)
        {
            if (dto == null)
            {
                return new TurnOrderSnapshot(0, Array.Empty<int>());
            }

            int current = TryGetIntProperty(dto, CurrentTurnUserIdPropertyNames);
            int[] order = TryGetIntArrayProperty(dto, OrderedAliveUserIdsPropertyNames);

            return new TurnOrderSnapshot(current, order);
        }

        private static int TryGetIntProperty(object dto, string[] names)
        {
            Type t = dto.GetType();

            foreach (string name in names)
            {
                PropertyInfo p = t.GetProperty(name);
                if (p == null)
                {
                    continue;
                }

                object value = p.GetValue(dto, null);
                if (value is int)
                {
                    return (int)value;
                }
            }

            foreach (PropertyInfo p in t.GetProperties())
            {
                if (p.PropertyType != typeof(int))
                {
                    continue;
                }

                string n = p.Name ?? string.Empty;
                if (n.IndexOf("Current", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("User", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    object value = p.GetValue(dto, null);
                    if (value is int)
                    {
                        return (int)value;
                    }
                }
            }

            return 0;
        }

        private static int[] TryGetIntArrayProperty(object dto, string[] names)
        {
            Type t = dto.GetType();

            int[] fromKnown = TryGetArrayFromKnownNames(dto, t, names);
            if (fromKnown.Length > 0)
            {
                return fromKnown;
            }

            int[] fromHeuristic = TryGetArrayFromHeuristic(dto, t);
            if (fromHeuristic.Length > 0)
            {
                return fromHeuristic;
            }

            return Array.Empty<int>();
        }

        private static int[] TryGetArrayFromKnownNames(object dto, Type t, string[] names)
        {
            foreach (string name in names)
            {
                PropertyInfo p = t.GetProperty(name);
                if (p == null)
                {
                    continue;
                }

                object value = p.GetValue(dto, null);
                int[] extracted = ExtractIntArray(value);
                if (extracted.Length > 0)
                {
                    return extracted;
                }
            }

            return Array.Empty<int>();
        }

        private static int[] TryGetArrayFromHeuristic(object dto, Type t)
        {
            foreach (PropertyInfo p in t.GetProperties())
            {
                if (!IsIntSequenceType(p.PropertyType))
                {
                    continue;
                }

                string n = p.Name ?? string.Empty;
                if (!LooksLikeOrderPropertyName(n))
                {
                    continue;
                }

                object value = p.GetValue(dto, null);
                int[] extracted = ExtractIntArray(value);
                if (extracted.Length > 0)
                {
                    return extracted;
                }
            }

            return Array.Empty<int>();
        }

        private static bool IsIntSequenceType(Type type)
        {
            return type == typeof(int[]) || typeof(IEnumerable<int>).IsAssignableFrom(type);
        }

        private static bool LooksLikeOrderPropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            return propertyName.IndexOf("Order", StringComparison.OrdinalIgnoreCase) >= 0
                || propertyName.IndexOf("Alive", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int[] ExtractIntArray(object value)
        {
            if (value == null)
            {
                return Array.Empty<int>();
            }

            int[] asIntArray = value as int[];
            if (asIntArray != null)
            {
                return asIntArray;
            }

            IEnumerable<int> asEnumerable = value as IEnumerable<int>;
            if (asEnumerable != null)
            {
                return asEnumerable.ToArray();
            }

            return Array.Empty<int>();
        }
    }
}

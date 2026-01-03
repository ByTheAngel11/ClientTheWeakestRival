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

            foreach (string name in names)
            {
                PropertyInfo p = t.GetProperty(name);
                if (p == null)
                {
                    continue;
                }

                object value = p.GetValue(dto, null);

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
            }

            foreach (PropertyInfo p in t.GetProperties())
            {
                if (p.PropertyType != typeof(int[]) &&
                    !typeof(IEnumerable<int>).IsAssignableFrom(p.PropertyType))
                {
                    continue;
                }

                string n = p.Name ?? string.Empty;
                if (n.IndexOf("Order", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Alive", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    object value = p.GetValue(dto, null);

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
                }
            }

            return Array.Empty<int>();
        }
    }
}

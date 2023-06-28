using System;

namespace VirtoCommerce.ElasticSearch8x.Tests
{
    public class PriorityAttribute : Attribute
    {
        public int Priority { get; }

        public PriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }
}

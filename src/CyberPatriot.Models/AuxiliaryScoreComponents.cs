using System;
using System.Collections.Immutable;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

namespace CyberPatriot.Models
{
    public struct AuxiliaryScoreComponents : IEnumerable<KeyValuePair<string, int>>, IEquatable<AuxiliaryScoreComponents>
    {
        public readonly ImmutableArray<string> ComponentNames;
        public readonly ImmutableArray<int> ComponentScores;

        public int Count => ComponentNames.IsDefaultOrEmpty ? 0 : ComponentNames.Length;

        public static readonly AuxiliaryScoreComponents Empty = new AuxiliaryScoreComponents(ImmutableArray<string>.Empty, ImmutableArray<int>.Empty);

        private AuxiliaryScoreComponents(ImmutableArray<string> componentNames, ImmutableArray<int> componentScores)
        {
            ComponentNames = componentNames;
            ComponentScores = componentScores;
        }

        [Newtonsoft.Json.JsonConstructor]
        public AuxiliaryScoreComponents(string[] componentNames, int[] componentScores)
        {
            if ((componentNames ?? throw new ArgumentNullException(nameof(componentNames))).Length != (componentScores ?? throw new ArgumentNullException(nameof(componentScores))).Length)
            {
                throw new ArgumentException("Auxilary score component corresponding arrays must be of equal length.");
            }

            if (componentNames.Any(x => x == null))
            {
                throw new ArgumentException("No auxilary component names may be null.");
            }

            ComponentNames = ImmutableArray.Create(componentNames);
            ComponentScores = ImmutableArray.Create(componentScores);
        }

        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return new KeyValuePair<string, int>(ComponentNames[i], ComponentScores[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(AuxiliaryScoreComponents other)
        {
            return other.ComponentNames.Equals(ComponentNames) && other.ComponentScores.Equals(ComponentScores);
        }

        public override bool Equals(object obj)
        {
            return obj is AuxiliaryScoreComponents s && Equals(s);
        }

        public override int GetHashCode()
        {
            int scoreHashCode = ComponentScores.GetHashCode();
            return ComponentNames.GetHashCode() ^ (scoreHashCode << 16) ^ (scoreHashCode >> 16);
        }
    }
}
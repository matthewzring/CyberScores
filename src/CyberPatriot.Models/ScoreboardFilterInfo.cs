using System;

namespace CyberPatriot.Models
{
    public struct ScoreboardFilterInfo
    {
        public static readonly ScoreboardFilterInfo NoFilter = new ScoreboardFilterInfo(null, null, null, null);

        public ScoreboardFilterInfo(Division? divFilter, Tier? tierFilter) : this(divFilter, tierFilter, null, null) { }

        public ScoreboardFilterInfo(Division? divFilter, Tier? tierFilter, ServiceCategory? categoryFilter, string locationFilter)
        {
            Division = divFilter;
            Tier = tierFilter;
            Category = categoryFilter;
            Location = locationFilter;
        }

        public Division? Division { get; private set; }
        public Tier? Tier { get; private set; }
        public ServiceCategory? Category { get; private set; }
        public string Location { get; private set; }

        public bool Matches(ScoreboardSummaryEntry teamSummary)
        {
            if (Division.HasValue && teamSummary.Division != Division.Value)
            {
                return false;
            }

            if (Tier.HasValue && teamSummary.Tier != Tier.Value)
            {
                return false;
            }

            if (Category.HasValue && teamSummary.Category != Category.Value)
            {
                return false;
            }

            if (Location != null && teamSummary.Location != Location)
            {
                return false;
            }

            return true;
        }

        public ScoreboardFilterInfo WithAdditionalFiltering(ScoreboardFilterInfo addition)
        {
            var newFilter = new ScoreboardFilterInfo();

            if (addition.Division.HasValue && Division.HasValue && addition.Division.Value != Division.Value)
            {
                throw new ArgumentException("An existing filter parameter cannot be changed.");
            }
            newFilter.Division = addition.Division ?? Division;

            if (addition.Tier.HasValue && Tier.HasValue && addition.Tier.Value != Tier.Value)
            {
                throw new ArgumentException("An existing filter parameter cannot be changed.");
            }
            newFilter.Tier = addition.Tier ?? Tier;

            if (addition.Category.HasValue && Category.HasValue && addition.Category.Value != Category.Value)
            {
                throw new ArgumentException("An existing filter parameter cannot be changed.");
            }
            newFilter.Category = addition.Category ?? Category;

            if (addition.Location != null && Location != null && addition.Location != Location)
            {
                throw new ArgumentException("An existing filter parameter cannot be changed.");
            }
            newFilter.Location = addition.Location ?? Location;

            return newFilter;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is ScoreboardFilterInfo other))
            {
                return false;
            }

            return Division == other.Division && Tier == other.Tier && Category == other.Category && Location == other.Location;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 19;
                hashCode = (hashCode * 401) ^ (Division.HasValue ? Division.Value.GetHashCode() : 0);
                hashCode = (hashCode * 401) ^ (Tier.HasValue ? Tier.Value.GetHashCode() : 0);
                hashCode = (hashCode * 401) ^ (Category.HasValue ? Category.Value.GetHashCode() : 0);
                hashCode = (hashCode * 401) ^ (Location != null ? Location.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(ScoreboardFilterInfo a, ScoreboardFilterInfo b)
        {
            // value type, no null check
            return a.Equals(b);
        }

        public static bool operator !=(ScoreboardFilterInfo a, ScoreboardFilterInfo b)
        {
            // value type, no null check
            return !a.Equals(b);
        }
    }
}
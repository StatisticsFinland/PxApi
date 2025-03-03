﻿using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// Information about a group of tables.
    /// </summary>
    public class TableGroup
    {
        /// <summary>
        /// The unique code for the group.
        /// </summary>
        [Required]
        public required string Code { get; set; }

        /// <summary>
        /// The name of the group.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// The unique code for the grouping this group belongs to.
        /// </summary>
        [Required]
        public required string GroupingCode { get; set; }

        /// <summary>
        /// The name of the grouping this group belongs to.
        /// </summary>
        [Required]
        public required string GroupingName { get; set; }

        /// <summary>
        /// Links to additional resources related to this group.
        /// </summary>
        [Required]
        public required List<Link> Links { get; set; }
    }
}

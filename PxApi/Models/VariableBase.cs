﻿using System.ComponentModel.DataAnnotations;

namespace PxApi.Models
{
    /// <summary>
    /// A base class for variables.
    /// </summary>
    public abstract class VariableBase
    {
        /// <summary>
        /// Unique identifier of the variable.
        /// </summary>
        [Required]
        public required string Code { get; set; }

        /// <summary>
        /// Name of the variable.
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// Additional information regarding the variable.
        /// </summary>
        public required string? Note { get; set; }

        /// <summary>
        /// How many values the variable has.
        /// </summary>
        [Range(1, int.MaxValue)]
        public required int Size { get; set; }

        /// <summary>
        /// Links to resources related to this variable.
        /// </summary>
        [Required]
        public required List<Link> Links { get; set; }
    }
}

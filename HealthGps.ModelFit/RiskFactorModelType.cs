using System.IO;

namespace HealthGps.ModelFit
{
    /// <summary>
    /// Store a risk factor model type definition.
    /// </summary>
    public readonly struct RiskFactorModelType
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="RiskFactorModelType"/> struct.
        /// </summary>
        /// <param name="id">The model type unique identifier.</param>
        /// <param name="filename">The model output filename.</param>
        /// <param name="dynamicFactor">Whether to include the dynamic factor.</param>
        public RiskFactorModelType(string id, FileInfo filename, bool dynamicFactor)
        {
            Identifier = id;
            Filename = filename;
            IncludeDynamicFactor = dynamicFactor;
        }

        /// <summary>
        /// Gets the model type unique identifier.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Gets the output filename
        /// </summary>
        public FileInfo Filename { get; }

        /// <summary>
        /// Gets a value indicating whether to include the dynamic factor.
        /// </summary>
        public bool IncludeDynamicFactor { get; }
    }
}

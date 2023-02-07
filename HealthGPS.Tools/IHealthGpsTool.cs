using System.IO;

namespace HealthGPS.Tools
{
    /// <summary>
    /// Health-GPS Tools interface
    /// </summary>
    public interface IHealthGpsTool
    {
        /// <summary>
        /// The tool name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The processing output logger
        /// </summary>
        TextWriter Logger { get; }

        /// <summary>
        ///  Execute the tool main action
        /// </summary>
        void Execute();
    }
}
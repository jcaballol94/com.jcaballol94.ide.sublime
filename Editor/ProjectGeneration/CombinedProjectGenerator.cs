
namespace jCaballol94.IDE.Sublime
{
    internal class CombinedProjectGenerator : ProjectGeneratorBase
    {
        private SublimeProjectGenerator m_sublimeGen;
        private SolutionGenerator m_solutionGen;

        public override string SolutionPath => m_sublimeGen.SolutionPath;
        public bool OmniSharpSupport => m_settings.SupportOmniSharp;

        public CombinedProjectGenerator() : base()
        {
            m_sublimeGen = new SublimeProjectGenerator(m_settings, m_tempFolder);
            m_solutionGen = new SolutionGenerator(m_settings, m_tempFolder);
        }

        public override void Sync()
        {
            if (m_settings.SupportOmniSharp)
            {
                m_solutionGen.Sync();
                m_sublimeGen.omniSharpSolution = m_solutionGen.SolutionPath;
            }
            else
                m_sublimeGen.omniSharpSolution = null;

            m_sublimeGen.Sync();
        }

        public override void SyncIfNeeded(string[] affectedFiles, string[] importedFiles)
        {
            if (m_settings.SupportOmniSharp)
            {
                m_solutionGen.SyncIfNeeded(affectedFiles, importedFiles);
                m_sublimeGen.omniSharpSolution = m_solutionGen.SolutionPath;
            }
            else
                m_sublimeGen.omniSharpSolution = null;
                
            m_sublimeGen.SyncIfNeeded(affectedFiles, importedFiles);
        }

        public override bool IsSupportedExtension(string extension)
        {
            return m_solutionGen.IsSupportedExtension(extension);
        }
    }
}
using System;
using Model;

namespace MetadataGenerator.Generation
{
    internal class ModuleGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public ModuleGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public void Generate(Assembly assembly)
        {
            var extension = assembly.Kind == AssemblyKind.Exe ? "exe" : "dll";
            var moduleName = $"{assembly.Name}.{extension}";
            var metadataBuilder = metadataContainer.MetadataBuilder;

            // Module Table (0x00) 
            metadataContainer.HandleResolver.ModuleHandle = metadataBuilder.AddModule(
                generation: 0,
                moduleName: metadataBuilder.GetOrAddString(moduleName),
                mvid: metadataBuilder.GetOrAddGuid(Guid.NewGuid()),
                encId: default,
                encBaseId: default);
        }
    }
}
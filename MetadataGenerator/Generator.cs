using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Model;
using Assembly = Model.Assembly;

namespace MetadataGenerator
{
    public class Generator : IGenerator
    {
        public void Generate(Assembly assembly)
        {
            using (var peStream = File.OpenWrite($"./{assembly.Name}.dll"))
            {
                var assemblyGenerator = AssemblyGenerator
                    .For(assembly)
                    .Generate();
                var peHeaderBuilder = new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll);
                var peBuilder = new ManagedPEBuilder(
                               header: peHeaderBuilder,
                               metadataRootBuilder: new MetadataRootBuilder(assemblyGenerator.ResolvedMetadata),
                               ilStream: assemblyGenerator.IlStream,
                               entryPoint: default(MethodDefinitionHandle), // dlls have no entry point
                               flags: CorFlags.ILOnly //FIXME  CorFlags.Requires32Bit | CorFlags.StrongNameSigned depend on dll
                              );
                var peBlob = new BlobBuilder();
                var contentId = peBuilder.Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }
        }
    }
}
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
            // using (var peStream = File.OpenWrite($"./{assembly.Name}.exe"))
            {
                var assemblyGenerator = AssemblyGenerator.For(assembly).Generate();
                var peHeaderBuilder = new PEHeaderBuilder(
                    imageCharacteristics: assemblyGenerator.Executable ? Characteristics.ExecutableImage : Characteristics.Dll
                    );
                var peBuilder = new ManagedPEBuilder(
                    header: peHeaderBuilder,
                    metadataRootBuilder: new MetadataRootBuilder(assemblyGenerator.GeneratedMetadata),
                    ilStream: assemblyGenerator.IlStream,
                    entryPoint: assemblyGenerator.MainMethodHandle ?? default(MethodDefinitionHandle),
                    flags: CorFlags.ILOnly // FIXME  CorFlags.Requires32Bit | CorFlags.StrongNameSigned depend on dll. Requires/prefers 32 bit?
                );
                var peBlob = new BlobBuilder();
                var contentId = peBuilder.Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }
        }
    }
}
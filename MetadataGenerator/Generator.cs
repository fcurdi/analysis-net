using System.IO;
using Model;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;
using SRPE = System.Reflection.PortableExecutable;

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
                var peHeaderBuilder = new SRPE.PEHeaderBuilder(
                    imageCharacteristics: assemblyGenerator.Executable ? SRPE.Characteristics.ExecutableImage : SRPE.Characteristics.Dll
                    );
                var peBlob = new SRM.BlobBuilder();
                new SRPE.ManagedPEBuilder(
                    header: peHeaderBuilder,
                    metadataRootBuilder: new ECMA335.MetadataRootBuilder(assemblyGenerator.GeneratedMetadata),
                    ilStream: assemblyGenerator.IlStream,
                    entryPoint: assemblyGenerator.MainMethodHandle ?? default,
                    // FIXME CorFlags.Requires32Bit | CorFlags.StrongNameSigned depend on dll. Requires/prefers 32 bit?
                    flags: SRPE.CorFlags.ILOnly).Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }
        }
    }
}
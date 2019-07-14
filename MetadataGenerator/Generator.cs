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

                var peHeaderBuilder = new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll); //FIXME
                var peBuilder = new ManagedPEBuilder(
                               header: peHeaderBuilder,
                               metadataRootBuilder: new MetadataRootBuilder(assemblyGenerator.ResolvedMetadata),
                               ilStream: assemblyGenerator.IlStream,
                               entryPoint: default(MethodDefinitionHandle), //FIXME
                               flags: CorFlags.ILOnly | CorFlags.StrongNameSigned, //FIXME
                               deterministicIdProvider: content => default(BlobContentId)); //FIXME
                var peBlob = new BlobBuilder();
                var contentId = peBuilder.Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }
        }
    }
}
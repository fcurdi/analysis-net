using System;
using System.IO;
using MetadataGenerator.Generation;
using Model;
using static Model.AssemblyKind;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;
using SRPE = System.Reflection.PortableExecutable;

namespace MetadataGenerator
{
    public class Generator : IGenerator
    {
        public void Generate(Assembly assembly)
        {
            var extension = assembly.Kind == Exe ? "exe" : "dll";
            var fileName = $"./{assembly.Name}.{extension}";
            Console.WriteLine($"Generating Console/bin/debug/{fileName.Substring(2)}");
            using (var peStream = File.OpenWrite(fileName))
            {
                var metadataContainer = AssemblyGenerator.Generate(assembly);
                var peHeaderBuilder = new SRPE.PEHeaderBuilder(
                    imageCharacteristics: assembly.Kind == Exe ? SRPE.Characteristics.ExecutableImage : SRPE.Characteristics.Dll
                );
                var peBlob = new SRM.BlobBuilder();
                new SRPE.ManagedPEBuilder(
                    header: peHeaderBuilder,
                    metadataRootBuilder: new ECMA335.MetadataRootBuilder(metadataContainer.MetadataBuilder),
                    ilStream: metadataContainer.MethodBodyStream.Builder,
                    mappedFieldData: metadataContainer.MappedFieldData,
                    entryPoint: assembly.Kind == Exe ? metadataContainer.HandleResolver.MainMethodHandle : default,
                    flags: SRPE.CorFlags.ILOnly | SRPE.CorFlags.StrongNameSigned
                ).Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }
        }
    }
}
using System;
using System.IO;
using MetadataGenerator.Generators;
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
            var extension = assembly.Kind == AssemblyKind.EXE ? "exe" : "dll";
            var fileName = $"./{assembly.Name}.{extension}";
            Console.WriteLine($"Generating Console/bin/debug/{fileName.Substring(2)}");
            using (var peStream = File.OpenWrite(fileName))
            {
                var metadataContainer = AssemblyGenerator.Generate(assembly);
                var peHeaderBuilder = new SRPE.PEHeaderBuilder(
                    imageCharacteristics: assembly.Kind == AssemblyKind.EXE
                        ? SRPE.Characteristics.ExecutableImage
                        : SRPE.Characteristics.Dll
                );
                var peBlob = new SRM.BlobBuilder();
                new SRPE.ManagedPEBuilder(
                    header: peHeaderBuilder,
                    metadataRootBuilder: new ECMA335.MetadataRootBuilder(metadataContainer.metadataBuilder),
                    ilStream: metadataContainer.methodBodyStream.Builder,
                    mappedFieldData: metadataContainer.mappedFieldData,
                    entryPoint: assembly.Kind.Equals(AssemblyKind.EXE) ? metadataContainer.MainMethodHandle : default,
                    flags: SRPE.CorFlags.ILOnly | SRPE.CorFlags.StrongNameSigned
                ).Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }
        }
    }
}
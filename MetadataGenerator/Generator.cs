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
            var fileName = $"./{assembly.Name}.dll";
            // var fileName = $"./{assembly.Name}.exe";
            using (var peStream = File.OpenWrite(fileName))
            {
                Console.WriteLine($"Processing: {fileName.Substring(2)}");
                var metadataContainer = AssemblyGenerator.Generate(assembly);
                var peHeaderBuilder = new SRPE.PEHeaderBuilder(
                    imageCharacteristics: metadataContainer.Executable
                        ? SRPE.Characteristics.ExecutableImage
                        : SRPE.Characteristics.Dll
                );
                var peBlob = new SRM.BlobBuilder();
                new SRPE.ManagedPEBuilder(
                    header: peHeaderBuilder,
                    metadataRootBuilder: new ECMA335.MetadataRootBuilder(metadataContainer.metadataBuilder),
                    ilStream: metadataContainer.methodBodyStream.Builder,
                    entryPoint: metadataContainer.MainMethodHandle ?? default
                ).Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }
        }
    }
}
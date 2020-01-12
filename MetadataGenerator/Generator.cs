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
        // TODO assembly should know if it is a dll or exe. With this i can make the generation more dynamic. Submit PR
        public void Generate(Assembly assembly)
        {
             var fileName = $"./{assembly.Name}(generated).dll";
            // var fileName = $"./{assembly.Name}(generated).exe";
            Console.WriteLine($"Generating Console/bin/debug/{fileName.Substring(2)}");
            using (var peStream = File.OpenWrite(fileName))
            {
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
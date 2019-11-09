using System;
using System.Collections.Generic;
using Model;
using ECMA335 = System.Reflection.Metadata.Ecma335;

// This relies on all other instructions are generated correctly. If not, labels don't match (because operations are missing)
namespace MetadataGenerator.Generators.Methods.Body
{
    class MethodBodyControlFlowGenerator
    {
        private readonly IDictionary<string, ECMA335.LabelHandle> labelHandles = new Dictionary<string, ECMA335.LabelHandle>();
        private readonly ECMA335.InstructionEncoder instructionEncoder;
        private readonly MetadataContainer metadataContainer;

        public MethodBodyControlFlowGenerator(ECMA335.InstructionEncoder instructionEncoder, MetadataContainer metadataContainer)
        {
            this.instructionEncoder = instructionEncoder;
            this.metadataContainer = metadataContainer;
        }

        // FIXME hacia afuera quiza no deberia estar esto y en cambio hablar de procesar los branchInstruction o algo asi
        // FIXME hay que pensar toda esta clase
        public ECMA335.LabelHandle LabelHandleFor(string label)
        {
            label = label.ToLower();
            if (labelHandles.TryGetValue(label, out var labelHandle))
            {
                return labelHandle;
            }
            else
            {
                labelHandle = instructionEncoder.DefineLabel();
                labelHandles.Add(label, labelHandle);


                // FIXME remove
                unmarkedLabels.Add(labelHandle);
                //


            }
            return labelHandle;
        }


        public void MarkCurrentLabel()
        {
            if (labelHandles.TryGetValue(instructionEncoder.CurrentLabelString(), out var labelHandle))
            {
                instructionEncoder.MarkLabel(labelHandle);

                //FIXME remove
                unmarkedLabels.Remove(labelHandle);
                //

            }
        }

        // Exception handling, relieson other instructions beign generated correctly. If not, labels don't match (because operations are missing)
        // FIXME name
        public void ProcessExceptionInformation(IList<ProtectedBlock> exceptionInformation)
        {
            var controlFlowBuilder = instructionEncoder.ControlFlowBuilder;
            foreach (var protectedBlock in exceptionInformation)
            {
                var tryStart = LabelHandleFor(protectedBlock.Start);
                var tryEnd = LabelHandleFor(protectedBlock.End);
                var handlerStart = LabelHandleFor(protectedBlock.Handler.Start);
                var handlerEnd = LabelHandleFor(protectedBlock.Handler.End);

                switch (protectedBlock.Handler.Kind)
                {
                    case ExceptionHandlerBlockKind.Filter:
                        var filterStart = LabelHandleFor(((FilterExceptionHandler)protectedBlock.Handler).FilterStart);
                        controlFlowBuilder.AddFilterRegion(tryStart, tryEnd, handlerStart, handlerEnd, filterStart);
                        break;
                    case ExceptionHandlerBlockKind.Catch:
                        var catchType = ((CatchExceptionHandler)protectedBlock.Handler).ExceptionType;
                        controlFlowBuilder.AddCatchRegion(tryStart, tryEnd, handlerStart, handlerEnd, metadataContainer.ResolveReferenceHandleFor(catchType));
                        break;
                    case ExceptionHandlerBlockKind.Fault:
                    case ExceptionHandlerBlockKind.Finally:
                        controlFlowBuilder.AddFinallyRegion(tryStart, tryEnd, handlerStart, handlerEnd);
                        break;
                }
            }
        }

        // FIXME only to develop while all instructions are not generated correctly because if some label results unmarked then 
        // SRM throws an exception. 
        private readonly IList<ECMA335.LabelHandle> unmarkedLabels = new List<ECMA335.LabelHandle> { };
        [Obsolete("Use only to develop while there are still instructions not being generated correclty")]
        public void MarkAllUnmarkedLabels()
        {
            foreach (var label in unmarkedLabels)
            {
                instructionEncoder.MarkLabel(label);

            }
        }
    }
}

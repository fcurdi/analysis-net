using System.Collections.Generic;
using Model;
using Model.Bytecode;
using ECMA335 = System.Reflection.Metadata.Ecma335;

namespace MetadataGenerator.Generation.Methods.Body
{
    internal class MethodBodyControlFlowGenerator
    {
        private readonly IDictionary<string, ECMA335.LabelHandle> labelHandles = new Dictionary<string, ECMA335.LabelHandle>();
        private readonly ECMA335.InstructionEncoder instructionEncoder;
        private readonly MetadataContainer metadataContainer;

        public MethodBodyControlFlowGenerator(ECMA335.InstructionEncoder instructionEncoder, MetadataContainer metadataContainer)
        {
            this.instructionEncoder = instructionEncoder;
            this.metadataContainer = metadataContainer;
        }

        public ECMA335.LabelHandle LabelHandleFor(string label)
        {
            label = label.ToLower();
            if (!labelHandles.TryGetValue(label, out var labelHandle))
            {
                labelHandle = instructionEncoder.DefineLabel();
                labelHandles.Add(label, labelHandle);
            }

            return labelHandle;
        }

        public void DefineNeededBranchLabels(IList<IInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction is BranchInstruction branchInstruction)
                {
                    LabelHandleFor(branchInstruction.Target);
                }
            }
        }

        public void MarkCurrentLabelIfNeeded(string label)
        {
            if (labelHandles.TryGetValue(label.ToLower(), out var labelHandle))
            {
                instructionEncoder.MarkLabel(labelHandle);
            }
        }

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
                        var filterStart = LabelHandleFor(((FilterExceptionHandler) protectedBlock.Handler).FilterStart);
                        controlFlowBuilder.AddFilterRegion(tryStart, tryEnd, handlerStart, handlerEnd, filterStart);
                        break;
                    case ExceptionHandlerBlockKind.Catch:
                        var catchType = ((CatchExceptionHandler) protectedBlock.Handler).ExceptionType;
                        controlFlowBuilder.AddCatchRegion(tryStart, tryEnd, handlerStart, handlerEnd,
                            metadataContainer.MetadataResolver.HandleOf(catchType));
                        break;
                    case ExceptionHandlerBlockKind.Fault:
                        controlFlowBuilder.AddFaultRegion(tryStart, tryEnd, handlerStart, handlerEnd);
                        break;
                    case ExceptionHandlerBlockKind.Finally:
                        controlFlowBuilder.AddFinallyRegion(tryStart, tryEnd, handlerStart, handlerEnd);
                        break;
                }
            }
        }
    }
}
using System;
using System.Linq;
using Backend.Utils;
using Model;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Visitor;
using Model.Types;
using ConvertInstruction = Model.ThreeAddressCode.Instructions.ConvertInstruction;
using CreateArrayInstruction = Model.ThreeAddressCode.Instructions.CreateArrayInstruction;
using CreateObjectInstruction = Model.ThreeAddressCode.Instructions.CreateObjectInstruction;
using IndirectMethodCallInstruction = Model.ThreeAddressCode.Instructions.IndirectMethodCallInstruction;
using LoadInstruction = Model.ThreeAddressCode.Instructions.LoadInstruction;
using LoadTokenInstruction = Model.ThreeAddressCode.Instructions.LoadTokenInstruction;
using MethodCallInstruction = Model.ThreeAddressCode.Instructions.MethodCallInstruction;
using SizeofInstruction = Model.ThreeAddressCode.Instructions.SizeofInstruction;
using StoreInstruction = Model.ThreeAddressCode.Instructions.StoreInstruction;
using SwitchInstruction = Model.ThreeAddressCode.Instructions.SwitchInstruction;
using Bytecode = Model.Bytecode;

// FIXME parti desde code-generator por las cosas que arregle en el metadata-provider. Esta en branch aparte. Estaria bueno hacer el PR de esto
// FIXME sobre el branch de metadata-provider sin los cambios del code-generator.
namespace Backend.Transformations
{
    public class Assembler
    {
        private readonly MethodDefinition method;

        public Assembler(MethodDefinition method)
        {
            if (!method.Body.Kind.Equals(MethodBodyKind.ThreeAddressCode))
            {
                throw new Exception("MethodBody must be in Three Address Code");
            }

            this.method = method;
        }

        public MethodBody Execute()
        {
            var body = new MethodBody(MethodBodyKind.Bytecode);

            body.MaxStack = method.Body.MaxStack;
            body.Parameters.AddRange(method.Body.Parameters);
            body.LocalVariables.AddRange(method.Body.LocalVariables);
            body.ExceptionInformation.AddRange(method.Body.ExceptionInformation);

            if (method.Body.Instructions.Count > 0)
            {
                new InstructionTranslator(body).Visit(method.Body);
            }

            return body;
        }


        //FIXME Revisar las operaciones que agregue (ej isinst, castClass, etc). Porque esas tampoco estan en las equivalentes del TAC y entonces no las puedo generar.
        // FIXME va a haber que agregarlas al TAC no solo a los enums sino distinguirlas al momento de crearlas (al traducir de bytecode que ahora las tiene a tac).

        // FIXME De la misma forma, hay algunas Instrucciones nuevas que habia metido creo. Y esas seguramente no esten en el Visitor por lo que tambien
        // FIXME va a haber que crearlas al traucir de bytecode a tac. (Ej indirectLoad/Store, Constrained, etc).

        // FIXME hay un par de metodos que no implemente porque son instrucciones de las que heredan otras por lo que entiendo que no hay que implementarlas.
        // FIXME pero tenerlo en cuenta por si despues falta algo, quiza habia alguna que si.
        private class InstructionTranslator : InstructionVisitor
        {
            private readonly MethodBody body;

            public InstructionTranslator(MethodBody body)
            {
                this.body = body;
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, OperationHelper.ToBasicOperation(instruction.Operation))
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(UnaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, OperationHelper.ToBasicOperation(instruction.Operation));
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(LoadInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(StoreInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(NopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Nop);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Breakpoint);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(TryInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(FaultInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(FinallyInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(CatchInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(ConvertInstruction instruction)
            {
                var convertInstruction = new Bytecode.ConvertInstruction(
                    instruction.Offset,
                    OperationHelper.ToConvertOperation(instruction.Operation),
                    instruction.ConversionType)
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands,
                };
                body.Instructions.Add(convertInstruction);
            }

            public override void Visit(ReturnInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Return);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(ThrowInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Throw);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                var unconditionalBranchInstruction = new Bytecode.BranchInstruction(
                    instruction.Offset,
                    Bytecode.BranchOperation.Branch,
                    Convert.ToUInt32(instruction.Target.Substring(2), 16));
                body.Instructions.Add(unconditionalBranchInstruction);
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                var conditionalBranchInstruction = new Bytecode.BranchInstruction(
                    instruction.Offset,
                    OperationHelper.ToBranchOperation(instruction.Operation),
                    Convert.ToUInt32(instruction.Target.Substring(2), 16));
                body.Instructions.Add(conditionalBranchInstruction);
            }

            public override void Visit(SwitchInstruction instruction)
            {
                var targets = instruction.Targets.Select(target => Convert.ToUInt32(target.Substring(2), 16)).ToList();
                var switchInstruction = new Bytecode.SwitchInstruction(instruction.Offset, targets);
                body.Instructions.Add(switchInstruction);
            }

            public override void Visit(SizeofInstruction instruction)
            {
                var sizeofInstruction = new Bytecode.SizeofInstruction(instruction.Offset, instruction.MeasuredType);
                body.Instructions.Add(sizeofInstruction);
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                var loadTokenInstruction = new Bytecode.LoadTokenInstruction(instruction.Offset, instruction.Token);
                body.Instructions.Add(loadTokenInstruction);
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(PhiInstruction instruction)
            {
                throw new Exception();
            }
        }
    }
}
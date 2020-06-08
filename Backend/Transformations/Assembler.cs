using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Backend.Utils;
using Model;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
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

            body.MaxStack = method.Body.MaxStack; // FIXME 
            body.Parameters.AddRange(method.Body.Parameters);
            body.LocalVariables.AddRange(method.Body.LocalVariables);

            if (method.Body.Instructions.Count > 0)
            {
                new InstructionTranslator(body).Visit(method.Body);
            }

            return body;
        }

        private class InstructionTranslator : InstructionVisitor
        {
            private readonly MethodBody body;
            private readonly Stack<ExceptionBlockBuilder> exceptionBlocks = new Stack<ExceptionBlockBuilder>();

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
                // translate to dup, loadArrayLength, loadArrayElement, loadArrayElementAddress, loadStaticField, loadInstanceField,
                // loadStaticFieldAddress, loadInnstanceFieldAddress, loadIndirect, loadConstant, loadVariable, loadVariableAddress, 
                // loadStaticMethodAddress, loadVirtualMethodAddress,
                // StoreInstruction? CreateObjectInstruction? (se genera una en el visit de ambos)
                // FIXME revisar los casos, hay algunos que no estoy seguro de que esten bien.

                Bytecode.Instruction loadInstruction;
                if (instruction.Operand is TemporalVariable && instruction.Result is TemporalVariable)
                {
                    if (instruction.Operand.Equals(instruction.Result))
                    {
                        return;
                    }
                    else
                    {
                        loadInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Dup);
                    }
                }
                else
                {
                    switch (instruction.Operand)
                    {
                        case Constant constant:
                            loadInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Value, constant);
                            break;
                        case TemporalVariable _:
                            loadInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Content, instruction.Result);
                            break;
                        case Dereference dereference:
                            loadInstruction = new Bytecode.LoadIndirectInstruction(instruction.Offset, dereference.Type);
                            break;
                        case Reference reference:
                            switch (reference.Value)
                            {
                                case ArrayElementAccess _:
                                case LocalVariable _:
                                    loadInstruction = new Bytecode.LoadInstruction(
                                        instruction.Offset,
                                        Bytecode.LoadOperation.Address,
                                        instruction.Result);
                                    break;
                                default:
                                    throw new Exception(); // TODO
                            }

                            break;
                        case LocalVariable localVariable:
                            loadInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Content, localVariable);
                            break;
                        case ArrayLengthAccess _:
                            loadInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.LoadArrayLength);
                            break;
                        case StaticMethodReference staticMethodReference:
                            loadInstruction = new Bytecode.LoadMethodAddressInstruction(
                                instruction.Offset,
                                Bytecode.LoadMethodAddressOperation.Static,
                                staticMethodReference.Method);
                            break;
                        case InstanceFieldAccess instanceFieldAccess:
                            // fixme es content?
                            loadInstruction = new Bytecode.LoadFieldInstruction(instruction.Offset, Bytecode.LoadFieldOperation.Content,
                                instanceFieldAccess.Field);
                            break;
                        case StaticFieldAccess staticFieldAccess:
                            // fixme es content?
                            loadInstruction = new Bytecode.LoadFieldInstruction(instruction.Offset, Bytecode.LoadFieldOperation.Content,
                                staticFieldAccess.Field);
                            break;
                        case ArrayElementAccess arrayElementAccess:
                            loadInstruction = new Bytecode.LoadArrayElementInstruction(
                                instruction.Offset,
                                Bytecode.LoadArrayElementOperation.Content,
                                (ArrayType) arrayElementAccess.Array.Type);
                            break;
                        default: throw new Exception(); // TODO
                    }
                }

                body.Instructions.Add(loadInstruction);
            }

            public override void Visit(StoreInstruction instruction)
            {
                Bytecode.Instruction storeInstruction;
                switch (instruction.Result)
                {
                    case ArrayElementAccess arrayElementAccess:
                        storeInstruction = new Bytecode.StoreArrayElementInstruction(instruction.Offset, (ArrayType) arrayElementAccess.Array.Type);
                        break;
                    case Dereference dereference:
                        storeInstruction = new Bytecode.StoreIndirectInstruction(instruction.Offset, dereference.Type);
                        break;
                    case InstanceFieldAccess instanceFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(instruction.Offset, instanceFieldAccess.Field);
                        break;
                    case StaticFieldAccess staticFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(instruction.Offset, staticFieldAccess.Field);
                        break;
                    default:
                        throw new Exception(); // TODO msg
                }

                body.Instructions.Add(storeInstruction);
            }

            public override void Visit(NopInstruction instruction)
            {
                var endsFilterOrFinally = exceptionBlocks.Count > 0 && (
                    ExceptionHandlerBlockKind.Filter.Equals(exceptionBlocks.Peek().HandlerBlockKind.Value) ||
                    ExceptionHandlerBlockKind.Finally.Equals(exceptionBlocks.Peek().HandlerBlockKind.Value));
                if (endsFilterOrFinally)
                {
                    var exceptionBlockBuilder = exceptionBlocks.Pop();
                    exceptionBlockBuilder.HandlerEnd = instruction.Offset;
                    body.ExceptionInformation.Add(exceptionBlockBuilder.Build());
                }
                else
                {
                    var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Nop);
                    body.Instructions.Add(basicInstruction);
                }
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Breakpoint);
                body.Instructions.Add(basicInstruction);
            }

            // TODO en el bytecode no hay instrucciones de todo lo que es exception handling. 
            // Hay que usar estas del tac que si hay para crear el exceptionInformation del method me parece usando las mismas para 
            // tener bien los labels en dodne estan. Hay que ver que los handlers y las excepciones si es que esta todo o habra que agregar algo.
            // la idea es que voy pusheando a la pila los bloques asi los voy completando y cuadno tengo la instruccion que lo termina (
            // la que sale del bloque) lo construyo, lo saco de la pila y lo agrego.
            // El tema es que
            //     - Para lo que es endFilter/endFinally, La traduccion de Bytecode a Tac genera un Nop. Hay que ver si lo que hice en el nop esta bien.
            //     Mismo con el leave. Ver esos metodos de extensions de CanFallThrough, IsExitingMethod, etc.

            public override void Visit(TryInstruction instruction)
            {
                var exceptionBlockBuilder = new ExceptionBlockBuilder {TryStart = instruction.Offset};
                exceptionBlocks.Push(exceptionBlockBuilder);
            }

            public override void Visit(FaultInstruction instruction)
            {
                var exceptionBlockBuilder = exceptionBlocks.Last();
                exceptionBlockBuilder.HandlerStart = instruction.Offset;
                exceptionBlockBuilder.HandlerBlockKind = ExceptionHandlerBlockKind.Fault;
            }

            public override void Visit(FinallyInstruction instruction)
            {
                var exceptionBlockBuilder = exceptionBlocks.Last();
                exceptionBlockBuilder.HandlerStart = instruction.Offset;
                exceptionBlockBuilder.HandlerBlockKind = ExceptionHandlerBlockKind.Finally;
            }

            public override void Visit(FilterInstruction instruction)
            {
                var exceptionBlockBuilder = exceptionBlocks.Last();
                exceptionBlockBuilder.HandlerStart = null; //FIXME
                exceptionBlockBuilder.HandlerBlockKind = ExceptionHandlerBlockKind.Filter;
                exceptionBlockBuilder.ExceptionType = instruction.ExceptionType;
            }

            public override void Visit(CatchInstruction instruction)
            {
                var exceptionBlockBuilder = exceptionBlocks.Last();
                exceptionBlockBuilder.HandlerStart = instruction.Offset;
                exceptionBlockBuilder.HandlerBlockKind = ExceptionHandlerBlockKind.Catch;
                exceptionBlockBuilder.ExceptionType = instruction.ExceptionType;
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
                if (exceptionBlocks.Count > 0)
                {
                    var exceptionBlockBuilder = exceptionBlocks.Peek();
                    if (exceptionBlockBuilder.HandlerStart == null) // fixme mismo comentario que en el leave
                    {
                        exceptionBlockBuilder.TryEnd = instruction.Offset;
                    }
                    else
                    {
                        // rethrow
                        exceptionBlockBuilder = exceptionBlocks.Pop();
                        exceptionBlockBuilder.HandlerEnd = instruction.Offset;
                        // exceptionBlockBuilder.ExceptionType = null; // FIXME ? al ser un rethrow, ya esta seteado esto por un anterior throw?
                        // fixme sin embargo en la rama del if, no habria que setear la excepcion?
                    }
                }
                else
                {
                    var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Throw);
                    body.Instructions.Add(basicInstruction);
                }
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                switch (instruction.Operation)
                {
                    // FIXME leave can be used for another purpose than exiting a protected region?
                    case UnconditionalBranchOperation.Leave when exceptionBlocks.Count == 0:
                    case UnconditionalBranchOperation.Branch:
                        var unconditionalBranchInstruction = new Bytecode.BranchInstruction(
                            instruction.Offset,
                            Bytecode.BranchOperation.Branch,
                            Convert.ToUInt32(instruction.Target.Substring(2), 16));
                        body.Instructions.Add(unconditionalBranchInstruction);
                        break;
                    case UnconditionalBranchOperation.Leave:
                        var exceptionBlockBuilder = exceptionBlocks.Peek();
                        if (exceptionBlockBuilder.HandlerStart == null) // FIXME estoy asumiendo que si no se seteo el handler es porque es un try aun
                            //  FIXME es correcto esto? Si lo es quiza puedo poner un metodo que diga que todavia estoy en el try asi se entiende mas
                        {
                            exceptionBlockBuilder.TryEnd = instruction.Offset;
                        }
                        else
                        {
                            exceptionBlockBuilder = exceptionBlocks.Pop();
                            exceptionBlockBuilder.HandlerEnd = instruction.Offset;
                            body.ExceptionInformation.Add(exceptionBlockBuilder.Build());
                        }

                        break;
                    default: throw instruction.Operation.ToUnknownValueException();
                }
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                var conditionalBranchInstruction = new Bytecode.BranchInstruction(
                    instruction.Offset,
                    OperationHelper.ToBranchOperation(instruction.Operation, instruction.RightOperand),
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
                var methodCallInstruction = new Bytecode.MethodCallInstruction(
                    instruction.Offset,
                    OperationHelper.ToMethodCallOperation(instruction.Operation),
                    instruction.Method
                );
                body.Instructions.Add(methodCallInstruction);
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                var indirectMethodCallInstruction = new Bytecode.IndirectMethodCallInstruction(instruction.Offset, instruction.Function);
                body.Instructions.Add(indirectMethodCallInstruction);
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                var createObjectInstruction = new Bytecode.CreateObjectInstruction(instruction.Offset, instruction.Constructor);
                body.Instructions.Add(createObjectInstruction);
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyBlock);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.LocalAllocation);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.InitBlock);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                var initObjInstruction = new Bytecode.InitObjInstruction(instruction.Offset, instruction.TargetAddress.Type);
                body.Instructions.Add(initObjInstruction);
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyObject);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                var createArrayInstruction =
                    new Bytecode.CreateArrayInstruction(instruction.Offset, new ArrayType(instruction.ElementType, instruction.Rank))
                    {
                        WithLowerBound = instruction.LowerBounds.Any()
                    };
                body.Instructions.Add(createArrayInstruction);
            }

            public override void Visit(PhiInstruction instruction)
            {
                throw new Exception();
            }

            private class ExceptionBlockBuilder
            {
                public uint? TryStart;
                public uint? TryEnd;
                public uint? HandlerStart;
                public uint? HandlerEnd;
                public ExceptionHandlerBlockKind? HandlerBlockKind;
                public IType ExceptionType;

                public ProtectedBlock Build()
                {
                    if (TryStart == null) throw new Exception("TryStart not set");
                    if (TryEnd == null) throw new Exception("TryEnd not set");
                    if (HandlerStart == null) throw new Exception("HandlerStart not set");
                    if (HandlerEnd == null) throw new Exception("HandlerEnd not set");
                    if (HandlerBlockKind == null) throw new Exception("HandlerBlockKind not set");
                    IExceptionHandler handler;
                    switch (HandlerBlockKind)
                    {
                        case ExceptionHandlerBlockKind.Filter:
                            if (ExceptionType == null) throw new Exception("ExceptionType not set");
                            handler = new FilterExceptionHandler(TryEnd.Value, HandlerStart.Value, HandlerEnd.Value, ExceptionType);
                            break;
                        case ExceptionHandlerBlockKind.Catch:
                            if (ExceptionType == null) throw new Exception("ExceptionType not set");
                            handler = new CatchExceptionHandler(HandlerStart.Value, HandlerEnd.Value, ExceptionType);
                            break;
                        case ExceptionHandlerBlockKind.Fault:
                            handler = new FaultExceptionHandler(HandlerStart.Value, HandlerEnd.Value);
                            break;
                        case ExceptionHandlerBlockKind.Finally:
                            handler = new FinallyExceptionHandler(HandlerStart.Value, HandlerEnd.Value);
                            break;
                        default: throw new UnknownValueException<ExceptionHandlerBlockKind>(HandlerBlockKind.Value);
                    }

                    return new ProtectedBlock(TryStart.Value, TryEnd.Value)
                    {
                        Handler = handler
                    };
                }
            }
        }
    }
}
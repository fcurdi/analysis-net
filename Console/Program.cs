// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using CCIProvider;
using MetadataProvider;
using Model;
using Model.Types;
using Backend.Analyses;
using Backend.Serialization;
using Backend.Transformations;
using Backend.Utils;
using Backend.Model;
using Backend.Transformations.Assembly;
using Model.ThreeAddressCode.Values;
using Tac = Model.ThreeAddressCode.Instructions;
using Bytecode = Model.Bytecode;

namespace Console
{
	class Program
	{
		private Host host;

		public Program(Host host)
		{
			this.host = host;
		}
		
		public void VisitMethods()
		{
			var allDefinedMethods = from a in host.Assemblies
									from t in a.RootNamespace.GetAllTypes()
									from m in t.Members.OfType<MethodDefinition>()
									where m.HasBody
									select m;

			foreach (var method in allDefinedMethods)
			{
				VisitMethod(method);
			}
		}

		private void VisitMethod(MethodDefinition method)
		{
			System.Console.WriteLine(method.ToSignatureString());

			var methodBodyBytecode = method.Body;
			var disassembler = new Disassembler(method);
			var methodBody = disassembler.Execute();			
			method.Body = methodBody;

			var cfAnalysis = new ControlFlowAnalysis(method.Body);
			//var cfg = cfAnalysis.GenerateNormalControlFlow();
			var cfg = cfAnalysis.GenerateExceptionalControlFlow();

			var dgml_CFG = DGMLSerializer.Serialize(cfg);

			var domAnalysis = new DominanceAnalysis(cfg);
			domAnalysis.Analyze();
			domAnalysis.GenerateDominanceTree();

			var loopAnalysis = new NaturalLoopAnalysis(cfg);
			loopAnalysis.Analyze();

			var domFrontierAnalysis = new DominanceFrontierAnalysis(cfg);
			domFrontierAnalysis.Analyze();

			var splitter = new WebAnalysis(cfg);
			splitter.Analyze();
			splitter.Transform();

			methodBody.UpdateVariables();

			var typeAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
			typeAnalysis.Analyze();

			// Copy Propagation
			var forwardCopyAnalysis = new ForwardCopyPropagationAnalysis(cfg);
			forwardCopyAnalysis.Analyze();
			forwardCopyAnalysis.Transform(methodBody);

			var backwardCopyAnalysis = new BackwardCopyPropagationAnalysis(cfg);
			backwardCopyAnalysis.Analyze();
			backwardCopyAnalysis.Transform(methodBody);

			// Points-To
			var pointsTo = new PointsToAnalysis(cfg, method);
			var result = pointsTo.Analyze();

			var ptg = result[cfg.Exit.Id].Output;
			//ptg.RemoveVariablesExceptParameters();
			ptg.RemoveTemporalVariables();

			var dgml_PTG = DGMLSerializer.Serialize(ptg);

			// Live Variables
			var liveVariables = new LiveVariablesAnalysis(cfg);
			var livenessInfo = liveVariables.Analyze();

			// SSA
			var ssa = new StaticSingleAssignment(methodBody, cfg);
			ssa.Transform();
			ssa.Prune(livenessInfo);

			methodBody.UpdateVariables();

			//var dot = DOTSerializer.Serialize(cfg);
			//var dgml = DGMLSerializer.Serialize(cfg);

			//dgml = DGMLSerializer.Serialize(host, typeDefinition);
		}

		private static void RunSomeTests()
		{
			const string root = @"..\..\..";
			//const string root = @"C:"; // casa
			//const string root = @"C:\Users\Edgar\Projects"; // facu

			const string input = root + @"\Test\bin\Debug\Test.dll";

			var host = new Host();
			//host.Assemblies.Add(assembly);

			PlatformTypes.Resolve(host);

			var loader = new Loader(host);
			loader.LoadAssembly(input);
			//loader.LoadCoreAssembly();

			var type = new BasicType("ExamplesPointsTo")
			{
				ContainingAssembly = new AssemblyReference("Test"),
				ContainingNamespace = "Test"
			};

			var typeDefinition = host.ResolveReference(type);

			var method = new MethodReference("Example1", PlatformTypes.Void)
			{
				ContainingType = type,
			};

			//var methodDefinition = host.ResolveReference(method) as MethodDefinition;

			var program = new Program(host);
			program.VisitMethods();

			// Testing method calls inlining
			var methodDefinition = host.ResolveReference(method) as MethodDefinition;
			var methodCalls = methodDefinition.Body.Instructions.OfType<Tac.MethodCallInstruction>().ToList();

			foreach (var methodCall in methodCalls)
			{
				var callee = host.ResolveReference(methodCall.Method) as MethodDefinition;
				methodDefinition.Body.Inline(methodCall, callee.Body);
			}

			methodDefinition.Body.UpdateVariables();

			type = new BasicType("ExamplesCallGraph")
			{
				ContainingAssembly = new AssemblyReference("Test"),
				ContainingNamespace = "Test"
			};

			method = new MethodReference("Example1", PlatformTypes.Void)
			{
				ContainingType = type,
			};

			methodDefinition = host.ResolveReference(method) as MethodDefinition;

			var ch = new ClassHierarchy();
			ch.Analyze(host);

			var dgml = DGMLSerializer.Serialize(ch);

			var chcga = new ClassHierarchyAnalysis(ch);
			var roots = host.GetRootMethods();
			var cg = chcga.Analyze(host, roots);

			dgml = DGMLSerializer.Serialize(cg);
		}

		private static void RunGenericsTests()
		{
			const string root = @"..\..\..";
			const string input = root + @"\Test\bin\Debug\Test.dll";

			var host = new Host();

			PlatformTypes.Resolve(host);

			var loader = new Loader(host);
			loader.LoadAssembly(input);
			//loader.LoadCoreAssembly();

			var assembly = new AssemblyReference("Test");

			var typeA = new GenericParameterReference(GenericParameterKind.Type, 0);
			var typeB = new GenericParameterReference(GenericParameterKind.Type, 1);

			var typeNestedClass = new BasicType("NestedClass")
			{
				ContainingAssembly = assembly,
				ContainingNamespace = "Test",
				GenericParameterCount = 2,
				ContainingType = new BasicType("ExamplesGenerics")
				{
					ContainingAssembly = assembly,
					ContainingNamespace = "Test",
					GenericParameterCount = 1
				}				
			};

			//typeNestedClass.ContainingType.GenericArguments.Add(typeA);
			//typeNestedClass.GenericArguments.Add(typeB);

			var typeDefinition = host.ResolveReference(typeNestedClass);

			if (typeDefinition == null)
			{
				System.Console.WriteLine("[Error] Cannot resolve type:\n{0}", typeNestedClass);
			}

			var typeK = new GenericParameterReference(GenericParameterKind.Method, 0);
			var typeV = new GenericParameterReference(GenericParameterKind.Method, 1);

			var typeKeyValuePair = new BasicType("KeyValuePair")
			{
				ContainingAssembly = new AssemblyReference("mscorlib"),
				ContainingNamespace = "System.Collections.Generic",
				GenericParameterCount = 2
			};

			typeKeyValuePair.GenericArguments.Add(typeK);
			typeKeyValuePair.GenericArguments.Add(typeV);

			var methodExampleGenericMethod = new MethodReference("ExampleGenericMethod", typeKeyValuePair)
			{
				ContainingType = typeNestedClass,
				GenericParameterCount = 2
			};

			//methodExampleGenericMethod.GenericArguments.Add(typeK);
			//methodExampleGenericMethod.GenericArguments.Add(typeV);

			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(0, typeA));
			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(1, typeB));
			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(2, typeK));
			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(3, typeV));
			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(4, typeKeyValuePair));

			var methodDefinition = host.ResolveReference(methodExampleGenericMethod) as MethodDefinition;

			if (methodDefinition == null)
			{
				System.Console.WriteLine("[Error] Cannot resolve method:\n{0}", methodExampleGenericMethod);
			}

			var methodExample = new MethodReference("Example", PlatformTypes.Void)
			{
				ContainingType = new BasicType("ExamplesGenericReferences")
				{
					ContainingAssembly = assembly,
					ContainingNamespace = "Test"
				}
			};

			methodDefinition = host.ResolveReference(methodExample) as MethodDefinition;

			if (methodDefinition == null)
			{
				System.Console.WriteLine("[Error] Cannot resolve method:\n{0}", methodExample);
			}

			var calls = methodDefinition.Body.Instructions.OfType<Bytecode.MethodCallInstruction>();

			foreach (var call in calls)
			{
				methodDefinition = host.ResolveReference(call.Method) as MethodDefinition;

				if (methodDefinition == null)
				{
					System.Console.WriteLine("[Error] Cannot resolve method:\n{0}", call.Method);
				}
			}
		}

		private static void RunInterPointsToTests()
		{
			const string root = @"..\..\..";
			const string input = root + @"\Test\bin\Debug\Test.dll";

			var host = new Host();

			PlatformTypes.Resolve(host);

			var loader = new Loader(host);
			loader.LoadAssembly(input);
			//loader.LoadCoreAssembly();

			var methodReference = new MethodReference("Example6", PlatformTypes.Void)
			//var methodReference = new MethodReference("Example6", PlatformTypes.Void)
			//var methodReference = new MethodReference("ExampleDelegateCaller", PlatformTypes.Void)
			{
				ContainingType = new BasicType("ExamplesPointsTo", TypeKind.ReferenceType)
				{
					ContainingAssembly = new AssemblyReference("Test"),
					ContainingNamespace = "Test"
				}
			};

			//var parameter = new MethodParameterReference(0, PlatformTypes.Boolean);
			//methodReference.Parameters.Add(parameter);
			//parameter = new MethodParameterReference(1, PlatformTypes.Boolean);
			//methodReference.Parameters.Add(parameter);

			//methodReference.ReturnType = new BasicType("Node", TypeKind.ReferenceType)
			//{
			//	ContainingAssembly = new AssemblyReference("Test"),
			//	ContainingNamespace = "Test"
			//};

			methodReference.Resolve(host);

			var programInfo = new ProgramAnalysisInfo();
			var pta = new InterPointsToAnalysis(programInfo);

			var cg = pta.Analyze(methodReference.ResolvedMethod);
			var dgml_CG = DGMLSerializer.Serialize(cg);

			//System.IO.File.WriteAllText(@"cg.dgml", dgml_CG);

			var esca = new EscapeAnalysis(programInfo, cg);
			var escapeResult = esca.Analyze();

			var fea = new FieldEffectsAnalysis(programInfo, cg);
			var effectsResult = fea.Analyze();

			foreach (var method in cg.Methods)
			{
				MethodAnalysisInfo methodInfo;
				var ok = programInfo.TryGet(method, out methodInfo);
				if (!ok) continue;

				InterPointsToInfo pti;
				ok = methodInfo.TryGet(InterPointsToAnalysis.INFO_IPTA_RESULT, out pti);

				if (ok)
				{
					var ptg = pti.Output;
					ptg.RemoveTemporalVariables();
					//ptg.RemoveVariablesExceptParameters();
					var dgml_PTG = DGMLSerializer.Serialize(ptg);

					//System.IO.File.WriteAllText(@"ptg.dgml", dgml_PTG);
				}

				EscapeInfo escapeInfo;
				ok = escapeResult.TryGetValue(method, out escapeInfo);

				FieldEffectsInfo effectsInfo;
				ok = effectsResult.TryGetValue(method, out effectsInfo);
			}
		}

		private static void HelloWorldAssembly()
		{
			var mscorlib = new AssemblyReference("mscorlib")
			{
				Version = new Version("4.0.0.0"),
				Culture = "",
				PublicKey = new byte[0]
			};
			var assembly = new Assembly("SampleAssembly", AssemblyKind.EXE)
			{
				Version = new Version("1.0.0.0"),
				PublicKey = new byte[0],
				Culture = "",
				References = {mscorlib}
			};
			var namezpace = new Namespace("MainNamespace") {ContainingAssembly = assembly};
			assembly.RootNamespace = namezpace;
			var type = new TypeDefinition("MainType", TypeKind.ReferenceType, TypeDefinitionKind.Class)
			{
				ContainingAssembly = assembly,
				ContainingNamespace = namezpace,
				Visibility = VisibilityKind.Public,
				IsStatic = true,
				Base = new BasicType("Object", TypeKind.ReferenceType)
				{
					ContainingAssembly = mscorlib,
					ContainingNamespace = "System",
				}
			};
			namezpace.Types.Add(type);
			var method = new MethodDefinition("Main", PlatformTypes.Void)
			{
				Visibility = VisibilityKind.Public,
				ContainingType = type,
				IsStatic = true,
				Body = new MethodBody(MethodBodyKind.Bytecode)
				{
					MaxStack = 1,
					Instructions =
					{
						new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Nop),
						new Bytecode.LoadInstruction(1, Bytecode.LoadOperation.Value, new Constant("Hello World!")),
						new Bytecode.MethodCallInstruction(2, Bytecode.MethodCallOperation.Static, ConsoleWriteLineMethodReference()),
						new Bytecode.BasicInstruction(3, Bytecode.BasicOperation.Return)
					}
				}
			};
			type.Methods.Add(method);
			var generator = new MetadataGenerator.Generator();
			generator.Generate(assembly);
		}

		private static void TacInstrumentation()
		{
			var input = "../../../ExamplesEXE/bin/Debug/ExamplesEXE.exe";
			var host = new Host();

			PlatformTypes.Resolve(host);

			System.Console.WriteLine($"Reading {input}");
			var loader = new MetadataProvider.Loader(host);
			loader.LoadAssembly(input);

			var main = (from a in host.Assemblies
				from t in a.RootNamespace.GetAllTypes()
				from m in t.Members.OfType<MethodDefinition>()
				where m.Name.Equals("Main")
				select m).First();

			var tac = new Backend.Transformations.Disassembler(main).Execute();
			main.Body = tac;

			AddLogAtIndex(0, "entering main", main.Body);
			AddLogAtIndex(tac.Instructions.Count - 1, "exiting main", main.Body);

			var cfanalysis = new ControlFlowAnalysis(main.Body);
			var cfg = cfanalysis.GenerateExceptionalControlFlow();

			var webAnalysis = new WebAnalysis(cfg);
			webAnalysis.Analyze();
			webAnalysis.Transform();
			main.Body.UpdateVariables();

			var typeInferenceAnalysis = new TypeInferenceAnalysis(cfg, main.ReturnType);
			typeInferenceAnalysis.Analyze();

			var bytecode = new Backend.Transformations.Assembly.Assembler(main).Execute();
			main.Body = bytecode;

			var generator = new MetadataGenerator.Generator();

			foreach (var assembly in host.Assemblies)
			{
				generator.Generate(assembly);
			}
		}

		private static void AddLogAtIndex(int index, string message, MethodBody body)
		{
			var result = new TemporalVariable("$s", 0);
			var loadInstruction = new Tac.LoadInstruction(0, result, new Constant(message))
			{
				Label = body.Instructions[index].Label + "º"
			};
			var methodCallInstruction = new Tac.MethodCallInstruction(
				0,
				null,
				Tac.MethodCallOperation.Static,
				ConsoleWriteLineMethodReference(),
				new List<IVariable> {result})
			{
				Label = loadInstruction.Label + "º"
			};

			var instructions =
				body.Instructions.Take(index)
					.Concat(new List<Tac.Instruction> {loadInstruction, methodCallInstruction})
					.Concat(body.Instructions.Skip(index))
					.ToList();

			body.Instructions.Clear();
			body.Instructions.AddRange(instructions);
		}

		private static MethodReference ConsoleWriteLineMethodReference()
		{
			var writeLineMethod = new MethodReference("WriteLine", PlatformTypes.Void)
			{
				Parameters = {new MethodParameter(0, "value", PlatformTypes.String)},
				IsStatic = true
			};
			var consoleType = new BasicType("Console", TypeKind.ReferenceType)
			{
				ContainingAssembly = new AssemblyReference("mscorlib") {Version = new Version("4.0.0.0")},
				ContainingNamespace = "System"
			};
			writeLineMethod.ContainingType = consoleType;
			return writeLineMethod;
		}

		private static void DisassembleAndThenAssemble()
		{
			// FIXME PROBAR generar y correr tests (+pedump) CONVIRTIENDO Y SIN CONVERTIR a tac
			var inputs = new[]
			{
				new[] {"../../../Examples/bin/Debug/Examples.dll"},
				new[]
				{
					"../../../../TinyCsvParser/TinyCsvParser/TinyCsvParser/bin/Debug/net45/TinyCsvParser.dll",
					"../../../../TinyCsvParser/TinyCsvParser/TinyCsvParser.Test/bin/Debug/net45/TinyCsvParser.Test.dll"
				},
				new[]
				{
					"../../../../DSA/DSA/DSA/bin/Debug/net45/DSA.dll",
					"../../../../DSA/DSA/DSAUnitTests/bin/Debug/net45/DSAUnitTests.dll"
				},
				new[]
				{
					"../../../../Fleck/src/Fleck.Tests/bin/Debug/net45/Fleck.dll",
					"../../../../Fleck/src/Fleck.Tests/bin/Debug/net45/Fleck.Tests.dll"
				},
				new[]
				{
					"../../../../Optional/src/Optional.Tests/bin/Debug/net45/Optional.dll",
					"../../../../Optional/src/Optional.Tests/bin/Debug/net45/Optional.Async.dll",
					"../../../../Optional/src/Optional.Tests/bin/Debug/net45/Optional.Tests.dll",
					"../../../../Optional/src/Optional.Tests/bin/Debug/net45/Optional.Utilities.dll"
				}
			};

			foreach (var file in inputs.SelectMany(i => i))
			{
				var host = new Host();

				PlatformTypes.Resolve(host);

				System.Console.WriteLine($"Reading {file}");
				var loader = new MetadataProvider.Loader(host);
				loader.LoadAssembly(file);

				var generator = new MetadataGenerator.Generator();

				foreach (var assembly in host.Assemblies)
				{
					generator.Generate(assembly);
				}
			}
		}

		private static void RemoveUnusedMethodFromSimpleExecutable()
		{
			var input = "../../../ExamplesEXE/bin/Debug/ExamplesEXE.exe";

			var host = new Host();

			PlatformTypes.Resolve(host);

			System.Console.WriteLine($"Reading {input}");
			var loader = new MetadataProvider.Loader(host);
			loader.LoadAssembly(input);
			
			var allDefinedMethods = (from a in host.Assemblies
				from t in a.RootNamespace.GetAllTypes()
				from m in t.Members.OfType<MethodDefinition>()
				select m).ToList();

			// convert bodies to typed tac
			foreach (var method in allDefinedMethods)
			{
				var tac = new Backend.Transformations.Disassembler(method).Execute();
				method.Body = tac;
				
				var cfanalysis = new ControlFlowAnalysis(method.Body);
				var cfg = cfanalysis.GenerateExceptionalControlFlow();

				var webAnalysis = new WebAnalysis(cfg);
				webAnalysis.Analyze();
				webAnalysis.Transform();
				method.Body.UpdateVariables();

				var typeInferenceAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
				typeInferenceAnalysis.Analyze();
			}
			
			// run call graph analysis
			var classHierarchy = new ClassHierarchy();
			classHierarchy.Analyze(host);
			var classHierarchyAnalysis = new ClassHierarchyAnalysis(classHierarchy);
			var roots = host.GetRootMethods();
			var callGraph = classHierarchyAnalysis.Analyze(host, roots);

			// convert back to bytecode for generation
			foreach (var method in allDefinedMethods)
			{
				var bytecode = new Backend.Transformations.Assembly.Assembler(method).Execute();
				method.Body = bytecode;
			}
			
			// remove unsued method
			var unusedMethod = allDefinedMethods.Except(callGraph.Methods).First();
			var type = (TypeDefinition) unusedMethod.ContainingType;
			var onlyUsedMethods = type.Methods.Where(method => !method.Equals(unusedMethod)).ToList();
			type.Methods.Clear();
			type.Methods.AddRange(onlyUsedMethods);

			// generate
			var generator = new MetadataGenerator.Generator();
			foreach (var assembly in host.Assemblies)
			{
				generator.Generate(assembly);
			}
		}

		static void Main(string[] args)
		{
			//DisassembleAndThenAssemble();
			//TacInstrumentation();
			// HelloWorldAssembly();
			RemoveUnusedMethodFromSimpleExecutable();
		
			System.Console.WriteLine("Done!");
		}
	}
}
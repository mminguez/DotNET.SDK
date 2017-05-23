﻿/*---------------------------------------------------------------------------------------------
 *  Copyright (c) 2008-2017 Dolittle. All rights reserved.
 *  Licensed under the MIT License. See LICENSE in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Bifrost.Applications;
using Bifrost.CodeGeneration;
using Bifrost.CodeGeneration.JavaScript;
using Bifrost.Commands;
using Bifrost.Execution;
using Bifrost.Extensions;
using Bifrost.Web.Configuration;
using Bifrost.Web.Proxies;

namespace Bifrost.Web.Commands
{
    public class CommandProxies : IProxyGenerator
    {
        internal static List<string> _namespacesToExclude = new List<string>();

        IApplicationResources _applicationResources;
        IApplicationResourceIdentifierConverter _applicationResourceIdentifierConverter;
        ITypeDiscoverer _typeDiscoverer;
        ITypeImporter _typeImporter;
        ICodeGenerator _codeGenerator;
        WebConfiguration _configuration;

        static CommandProxies()
        {
            ExcludeCommandsStartingWithNamespace("Bifrost");
        }

        public static void ExcludeCommandsStartingWithNamespace(string @namespace)
        {
            _namespacesToExclude.Add(@namespace);
        }

        public CommandProxies(
            IApplicationResources applicationResources,
            IApplicationResourceIdentifierConverter applicationResourceIdentifierConverter, 
            ITypeDiscoverer typeDiscoverer, 
            ITypeImporter typeImporter, 
            ICodeGenerator codeGenerator, 
            WebConfiguration configuration)
        {
            _applicationResources = applicationResources;
            _applicationResourceIdentifierConverter = applicationResourceIdentifierConverter;
            _typeDiscoverer = typeDiscoverer;
            _typeImporter = typeImporter;
            _codeGenerator = codeGenerator;
            
            _configuration = configuration;
        }

        public string Generate()
        {
            var typesByNamespace = _typeDiscoverer.FindMultiple<ICommand>().Where(t => !_namespacesToExclude.Any(n => t.Namespace.StartsWith(n))).GroupBy(t=>t.Namespace);
            var commandPropertyExtenders = _typeImporter.ImportMany<ICanExtendCommandProperty>();

            var result = new StringBuilder();

            Namespace currentNamespace;
            Namespace globalCommands = _codeGenerator.Namespace(Namespaces.COMMANDS);

            foreach (var @namespace in typesByNamespace)
            {
                if (_configuration.NamespaceMapper.CanResolveToClient(@namespace.Key))
                    currentNamespace = _codeGenerator.Namespace(_configuration.NamespaceMapper.GetClientNamespaceFrom(@namespace.Key));
                else
                    currentNamespace = globalCommands;
                
                foreach (var type in @namespace)
                {
                    if (type.GetTypeInfo().IsGenericType) continue;

                    var identifier = _applicationResources.Identify(type);
                    var identifierAsString = _applicationResourceIdentifierConverter.AsString(identifier);

                    var name = ((string)identifier.Resource.Name).ToCamelCase();
                    currentNamespace.Content.Assign(name)
                        .WithType(t =>
                            t.WithSuper("Bifrost.commands.Command")
                                .Function
                                    .Body
                                        .Variant("self", v => v.WithThis())
                                        .Property("_commandType", p => p.WithString(identifierAsString))

                                        .WithObservablePropertiesFrom(type, excludePropertiesFrom: typeof(ICommand), observableVisitor: (propertyName, observable) =>
                                        {
                                            foreach (var commandPropertyExtender in commandPropertyExtenders)
                                                commandPropertyExtender.Extend(type, propertyName, observable);
                                        }));
                }

                if (currentNamespace != globalCommands)
                    result.Append(_codeGenerator.GenerateFrom(currentNamespace));
            }
            result.Append(_codeGenerator.GenerateFrom(globalCommands));
            
            return result.ToString();
        }
    }
}

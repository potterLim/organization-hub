using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using OrganizationHub.Web.Domain;
using Xunit;

namespace OrganizationHub.Web.Tests.Architecture;

public sealed class ArchitectureRulesTests
{
    private const string DOMAIN_NAMESPACE_PREFIX = "OrganizationHub.Web.Domain";
    private const string MODEL_NAMESPACE_PREFIX = "OrganizationHub.Web.Models";
    private const string SERVICE_NAMESPACE_PREFIX = "OrganizationHub.Web.Services";
    private const string DATA_NAMESPACE_PREFIX = "OrganizationHub.Web.Data";
    private const string SOLUTION_FILE_NAME = "OrganizationHub.sln";
    private const string SOURCE_ROOT_FOLDER_NAME = "src";
    private const string WEB_PROJECT_FOLDER_NAME = "OrganizationHub.Web";
    private const string DOMAIN_SOURCE_FOLDER_NAME = "Domain";
    private const string MODEL_SOURCE_FOLDER_NAME = "Models";
    private const string SERVICE_SOURCE_FOLDER_NAME = "Services";
    private const string DATA_SOURCE_FOLDER_NAME = "Data";
    private const string CSHARP_FILE_SEARCH_PATTERN = "*.cs";
    private const string USING_DIRECTIVE_PREFIX = "using ";
    private const string STATIC_USING_TARGET_PREFIX = "static ";
    private const string GENERATED_FILE_SUFFIX = ".g.cs";
    private const string DESIGNER_FILE_SUFFIX = ".Designer.cs";
    private const char USING_ALIAS_SEPARATOR = '=';
    private const char USING_DIRECTIVE_SUFFIX = ';';
    private const int NEXT_CHARACTER_INDEX_OFFSET = 1;
    private const int LINE_NUMBER_OFFSET = 1;
    private const int USING_DIRECTIVE_SUFFIX_LENGTH = 1;

    private static readonly IReadOnlySet<string> DOMAIN_FORBIDDEN_NAMESPACE_PREFIXES = new HashSet<string>(StringComparer.Ordinal)
    {
        "Google.Apis",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "MudBlazor",
        "OrganizationHub.Web.Components",
        "OrganizationHub.Web.Configuration",
        "OrganizationHub.Web.Controllers",
        "OrganizationHub.Web.Data",
        "OrganizationHub.Web.Models",
        "OrganizationHub.Web.Services"
    };

    private static readonly IReadOnlySet<string> APPLICATION_MODEL_FORBIDDEN_NAMESPACE_PREFIXES = new HashSet<string>(StringComparer.Ordinal)
    {
        "Google.Apis",
        "Microsoft.AspNetCore.Components",
        "Microsoft.EntityFrameworkCore",
        "MudBlazor",
        "OrganizationHub.Web.Components",
        "OrganizationHub.Web.Controllers",
        "OrganizationHub.Web.Data",
        "OrganizationHub.Web.Services"
    };

    private static readonly IReadOnlySet<string> APPLICATION_SERVICE_FORBIDDEN_NAMESPACE_PREFIXES = new HashSet<string>(StringComparer.Ordinal)
    {
        "Microsoft.AspNetCore.Components",
        "MudBlazor",
        "OrganizationHub.Web.Components",
        "OrganizationHub.Web.Controllers"
    };

    [Fact]
    public void DomainTypesDoNotDependOnApplicationOrInfrastructureLayers()
    {
        IReadOnlyList<string> invalidReferences = findInvalidReferences(
            DOMAIN_NAMESPACE_PREFIX,
            DOMAIN_FORBIDDEN_NAMESPACE_PREFIXES);

        Assert.Empty(invalidReferences);
    }

    [Fact]
    public void ModelTypesDoNotDependOnApplicationOrInfrastructureLayers()
    {
        IReadOnlyList<string> invalidReferences = findInvalidReferences(
            MODEL_NAMESPACE_PREFIX,
            APPLICATION_MODEL_FORBIDDEN_NAMESPACE_PREFIXES);

        Assert.Empty(invalidReferences);
    }

    [Fact]
    public void ServiceTypesDoNotDependOnUiLayers()
    {
        IReadOnlyList<string> invalidReferences = findInvalidReferences(
            SERVICE_NAMESPACE_PREFIX,
            APPLICATION_SERVICE_FORBIDDEN_NAMESPACE_PREFIXES);

        Assert.Empty(invalidReferences);
    }

    [Fact]
    public void DataTypesDoNotDependOnUiLayers()
    {
        IReadOnlyList<string> invalidReferences = findInvalidReferences(
            DATA_NAMESPACE_PREFIX,
            APPLICATION_SERVICE_FORBIDDEN_NAMESPACE_PREFIXES);

        Assert.Empty(invalidReferences);
    }

    [Fact]
    public void DomainSourceFilesDoNotImportApplicationOrInfrastructureLayers()
    {
        IReadOnlyList<string> invalidReferences = findInvalidUsingDirectiveReferences(
            DOMAIN_SOURCE_FOLDER_NAME,
            DOMAIN_FORBIDDEN_NAMESPACE_PREFIXES);

        Assert.Empty(invalidReferences);
    }

    [Fact]
    public void ModelSourceFilesDoNotImportApplicationOrInfrastructureLayers()
    {
        IReadOnlyList<string> invalidReferences = findInvalidUsingDirectiveReferences(
            MODEL_SOURCE_FOLDER_NAME,
            APPLICATION_MODEL_FORBIDDEN_NAMESPACE_PREFIXES);

        Assert.Empty(invalidReferences);
    }

    [Fact]
    public void ServiceSourceFilesDoNotImportUiLayers()
    {
        IReadOnlyList<string> invalidReferences = findInvalidUsingDirectiveReferences(
            SERVICE_SOURCE_FOLDER_NAME,
            APPLICATION_SERVICE_FORBIDDEN_NAMESPACE_PREFIXES);

        Assert.Empty(invalidReferences);
    }

    [Fact]
    public void DataSourceFilesDoNotImportUiLayers()
    {
        IReadOnlyList<string> invalidReferences = findInvalidUsingDirectiveReferences(
            DATA_SOURCE_FOLDER_NAME,
            APPLICATION_SERVICE_FORBIDDEN_NAMESPACE_PREFIXES);

        Assert.Empty(invalidReferences);
    }

    private static IReadOnlyList<string> findInvalidReferences(
        string sourceNamespacePrefix,
        IReadOnlySet<string> forbiddenNamespacePrefixes
    )
    {
        List<string> invalidReferences = new List<string>();
        IReadOnlyList<Type> applicationTypes = getApplicationTypes();
        foreach (Type sourceType in applicationTypes)
        {
            if (isTypeInNamespace(sourceType, sourceNamespacePrefix) == false)
            {
                continue;
            }

            IReadOnlyList<Type> referencedTypes = findReferencedTypes(sourceType);
            foreach (Type referencedType in referencedTypes)
            {
                string referencedNamespace = string.Empty;
                if (referencedType.Namespace != null)
                {
                    referencedNamespace = referencedType.Namespace;
                }

                if (hasForbiddenNamespacePrefix(referencedNamespace, forbiddenNamespacePrefixes) == false)
                {
                    continue;
                }

                invalidReferences.Add(formatInvalidReference(sourceType, referencedType));
            }
        }

        return invalidReferences
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> findInvalidUsingDirectiveReferences(
        string sourceFolderName,
        IReadOnlySet<string> forbiddenNamespacePrefixes
    )
    {
        List<string> invalidReferences = new List<string>();
        string sourceFolderPath = findSourceFolderPath(sourceFolderName);
        if (Directory.Exists(sourceFolderPath) == false)
        {
            throw new DirectoryNotFoundException(sourceFolderPath);
        }

        IReadOnlyList<string> sourceFilePaths = Directory
            .GetFiles(sourceFolderPath, CSHARP_FILE_SEARCH_PATTERN, SearchOption.AllDirectories)
            .OrderBy(sourceFilePath => sourceFilePath, StringComparer.Ordinal)
            .ToList();

        foreach (string sourceFilePath in sourceFilePaths)
        {
            if (isGeneratedSourceFile(sourceFilePath))
            {
                continue;
            }

            IReadOnlyList<string> sourceLines = File.ReadAllLines(sourceFilePath);
            for (int lineIndex = 0; lineIndex < sourceLines.Count; lineIndex++)
            {
                string usingNamespace = getUsingNamespace(sourceLines[lineIndex]);
                if (string.IsNullOrWhiteSpace(usingNamespace))
                {
                    continue;
                }

                if (hasForbiddenNamespacePrefix(usingNamespace, forbiddenNamespacePrefixes) == false)
                {
                    continue;
                }

                invalidReferences.Add(formatInvalidUsingDirectiveReference(sourceFilePath, lineIndex, usingNamespace));
            }
        }

        return invalidReferences;
    }

    private static string findSourceFolderPath(string sourceFolderName)
    {
        string repositoryRoot = findRepositoryRoot();
        return Path.Combine(repositoryRoot, SOURCE_ROOT_FOLDER_NAME, WEB_PROJECT_FOLDER_NAME, sourceFolderName);
    }

    private static string findRepositoryRoot()
    {
        string currentDirectory = AppContext.BaseDirectory;
        while (true)
        {
            string solutionFilePath = Path.Combine(currentDirectory, SOLUTION_FILE_NAME);
            if (File.Exists(solutionFilePath))
            {
                return currentDirectory;
            }

            DirectoryInfo? parentDirectoryOrNull = Directory.GetParent(currentDirectory);
            if (parentDirectoryOrNull == null)
            {
                throw new InvalidOperationException("Repository root was not found from " + AppContext.BaseDirectory);
            }

            currentDirectory = parentDirectoryOrNull.FullName;
        }
    }

    private static bool isGeneratedSourceFile(string sourceFilePath)
    {
        string fileName = Path.GetFileName(sourceFilePath);
        return fileName.EndsWith(GENERATED_FILE_SUFFIX, StringComparison.Ordinal)
            || fileName.EndsWith(DESIGNER_FILE_SUFFIX, StringComparison.Ordinal);
    }

    private static string getUsingNamespace(string sourceLine)
    {
        string trimmedLine = sourceLine.Trim();
        if (trimmedLine.StartsWith(USING_DIRECTIVE_PREFIX, StringComparison.Ordinal) == false)
        {
            return string.Empty;
        }

        if (trimmedLine.EndsWith(USING_DIRECTIVE_SUFFIX) == false)
        {
            return string.Empty;
        }

        string usingTarget = trimmedLine.Substring(USING_DIRECTIVE_PREFIX.Length);
        usingTarget = usingTarget.Substring(0, usingTarget.Length - USING_DIRECTIVE_SUFFIX_LENGTH).Trim();

        int aliasSeparatorIndex = usingTarget.IndexOf(USING_ALIAS_SEPARATOR, StringComparison.Ordinal);
        if (aliasSeparatorIndex >= 0)
        {
            usingTarget = usingTarget.Substring(aliasSeparatorIndex + NEXT_CHARACTER_INDEX_OFFSET).Trim();
        }

        if (usingTarget.StartsWith(STATIC_USING_TARGET_PREFIX, StringComparison.Ordinal))
        {
            usingTarget = usingTarget.Substring(STATIC_USING_TARGET_PREFIX.Length).Trim();
        }

        return usingTarget;
    }

    private static IReadOnlyList<Type> getApplicationTypes()
    {
        return typeof(PermissionCatalog).Assembly.GetTypes();
    }

    private static bool isTypeInNamespace(Type type, string namespacePrefix)
    {
        string typeNamespace = string.Empty;
        if (type.Namespace != null)
        {
            typeNamespace = type.Namespace;
        }

        return typeNamespace.StartsWith(namespacePrefix, StringComparison.Ordinal);
    }

    private static bool hasForbiddenNamespacePrefix(
        string referencedNamespace,
        IReadOnlySet<string> forbiddenNamespacePrefixes
    )
    {
        foreach (string forbiddenNamespacePrefix in forbiddenNamespacePrefixes)
        {
            if (referencedNamespace.StartsWith(forbiddenNamespacePrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<Type> findReferencedTypes(Type sourceType)
    {
        List<Type> referencedTypes = new List<Type>();
        addReferencedType(referencedTypes, sourceType.BaseType);

        foreach (Type interfaceType in sourceType.GetInterfaces())
        {
            addReferencedType(referencedTypes, interfaceType);
        }

        BindingFlags bindingFlags = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        addFieldTypes(referencedTypes, sourceType, bindingFlags);
        addPropertyTypes(referencedTypes, sourceType, bindingFlags);
        addConstructorTypes(referencedTypes, sourceType, bindingFlags);
        addMethodTypes(referencedTypes, sourceType, bindingFlags);

        return referencedTypes;
    }

    private static void addFieldTypes(List<Type> referencedTypes, Type sourceType, BindingFlags bindingFlags)
    {
        foreach (FieldInfo fieldInfo in sourceType.GetFields(bindingFlags))
        {
            addReferencedType(referencedTypes, fieldInfo.FieldType);
        }
    }

    private static void addPropertyTypes(List<Type> referencedTypes, Type sourceType, BindingFlags bindingFlags)
    {
        foreach (PropertyInfo propertyInfo in sourceType.GetProperties(bindingFlags))
        {
            addReferencedType(referencedTypes, propertyInfo.PropertyType);
        }
    }

    private static void addConstructorTypes(List<Type> referencedTypes, Type sourceType, BindingFlags bindingFlags)
    {
        foreach (ConstructorInfo constructorInfo in sourceType.GetConstructors(bindingFlags))
        {
            addParameterTypes(referencedTypes, constructorInfo.GetParameters());
        }
    }

    private static void addMethodTypes(List<Type> referencedTypes, Type sourceType, BindingFlags bindingFlags)
    {
        foreach (MethodInfo methodInfo in sourceType.GetMethods(bindingFlags))
        {
            addReferencedType(referencedTypes, methodInfo.ReturnType);
            addParameterTypes(referencedTypes, methodInfo.GetParameters());
        }
    }

    private static void addParameterTypes(List<Type> referencedTypes, IReadOnlyList<ParameterInfo> parameterInfos)
    {
        foreach (ParameterInfo parameterInfo in parameterInfos)
        {
            addReferencedType(referencedTypes, parameterInfo.ParameterType);
        }
    }

    private static void addReferencedType(List<Type> referencedTypes, Type? referencedTypeOrNull)
    {
        if (referencedTypeOrNull == null)
        {
            return;
        }

        if (referencedTypeOrNull.IsArray)
        {
            addReferencedType(referencedTypes, referencedTypeOrNull.GetElementType());
            return;
        }

        if (referencedTypeOrNull.IsByRef || referencedTypeOrNull.IsPointer)
        {
            addReferencedType(referencedTypes, referencedTypeOrNull.GetElementType());
            return;
        }

        if (referencedTypeOrNull.IsGenericParameter)
        {
            return;
        }

        referencedTypes.Add(referencedTypeOrNull);

        if (referencedTypeOrNull.IsGenericType)
        {
            foreach (Type genericArgumentType in referencedTypeOrNull.GetGenericArguments())
            {
                addReferencedType(referencedTypes, genericArgumentType);
            }
        }
    }

    private static string formatInvalidReference(Type sourceType, Type referencedType)
    {
        string sourceTypeName = string.Empty;
        if (sourceType.FullName != null)
        {
            sourceTypeName = sourceType.FullName;
        }

        string referencedTypeName = string.Empty;
        if (referencedType.FullName != null)
        {
            referencedTypeName = referencedType.FullName;
        }

        return sourceTypeName + " -> " + referencedTypeName;
    }

    private static string formatInvalidUsingDirectiveReference(
        string sourceFilePath,
        int lineIndex,
        string usingNamespace
    )
    {
        string repositoryRoot = findRepositoryRoot();
        string relativeFilePath = Path.GetRelativePath(repositoryRoot, sourceFilePath);
        int lineNumber = lineIndex + LINE_NUMBER_OFFSET;
        return relativeFilePath + ":" + lineNumber.ToString(CultureInfo.InvariantCulture) + " -> " + usingNamespace;
    }
}

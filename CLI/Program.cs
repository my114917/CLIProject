using System.CommandLine;
using System.IO;

var outputBundleOption = new Option<FileInfo>(new[] { "--output", "-o" }, "File path and name");

var languageOption = new Option<string>(new[] { "--languages", "-l" }, "List of programming languages to include in the bundle (comma-separated)")
{
    IsRequired = true
};

var noteOption = new Option<bool>(new[] { "--note", "-n" }, "Include source file paths as comments in the bundle file")
{
    IsRequired = false
};

var sortOption = new Option<string>(new[] { "--sort", "-s" }, "Sort order: 'name' (default) or 'type'")
{
    IsRequired = false,
    ArgumentHelpName = "sort-order",
    Description = "Sort files by 'name' (alphabetically) or 'type' (file extension)."
};
sortOption.SetDefaultValue("name");

var removeEmptyLinesOption = new Option<bool>(new[] { "--remove-empty-lines", "-r" }, "Remove empty lines from the source files")
{
    IsRequired = false
};

var authorOption = new Option<string>(new[] { "--author", "-a" }, "Name of the author to include as a comment in the bundle file")
{
    IsRequired = false
};

var bundleCommand = new Command("bundle", "Bundle code files to a single file");
bundleCommand.AddOption(outputBundleOption);
bundleCommand.AddOption(languageOption);
bundleCommand.AddOption(noteOption);
bundleCommand.AddOption(sortOption);
bundleCommand.AddOption(removeEmptyLinesOption);
bundleCommand.AddOption(authorOption);

bundleCommand.SetHandler((FileInfo output, string languages, bool note, string sort, bool removeEmptyLines, string author) =>
{
    try
    {
        var selectedLanguages = languages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                         .Select(lang => lang.Trim())
                                         .ToArray();

        var currentDirectory = new DirectoryInfo(Environment.CurrentDirectory);
        var allFiles = currentDirectory.GetFiles("*", SearchOption.AllDirectories);
        IEnumerable<FileInfo> filesToInclude;

        if (selectedLanguages.Length == 1 && selectedLanguages[0].ToLower() == "all")
        {
            Console.WriteLine("Including all files...");
            filesToInclude = allFiles;
        }
        else
        {
            Console.WriteLine("Including files for languages: " + string.Join(", ", selectedLanguages));
            var validExtensions = selectedLanguages.Select(lang => $".{lang.ToLower()}");
            filesToInclude = allFiles.Where(file => validExtensions.Contains(file.Extension.ToLower()));
        }

        filesToInclude = sort.ToLower() switch
        {
            "type" => filesToInclude.OrderBy(file => file.Extension.TrimStart('.').ToLower()).ThenBy(file => file.Name.ToLower()),
            _ => filesToInclude.OrderBy(file => file.Name.ToLower())
        };

        using (var outputStream = File.Create(output.FullName))
        using (var writer = new StreamWriter(outputStream))
        {
            if (!string.IsNullOrWhiteSpace(author))
            {
                writer.WriteLine($"// Author: {author}");
            }

            foreach (var file in filesToInclude)
            {
                if (note)
                {
                    var relativePath = Path.GetRelativePath(currentDirectory.FullName, file.FullName);
                    writer.WriteLine($"// Source: {relativePath}");
                }

                var content = File.ReadAllLines(file.FullName);
                if (removeEmptyLines)
                {
                    content = content.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                }

                foreach (var line in content)
                {
                    writer.WriteLine(line);
                }
                writer.WriteLine();
            }
        }

        Console.WriteLine("File was created");
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine("File path is invalid");
    }
}, outputBundleOption, languageOption, noteOption, sortOption, removeEmptyLinesOption, authorOption);

var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");
var rspFileOption = new Option<FileInfo>(new[] { "--rsp-file", "-r" }, "Path to the response file to be created")
{
    IsRequired = true
};
createRspCommand.AddOption(rspFileOption);

createRspCommand.SetHandler((FileInfo rspFile) =>
{
    Console.WriteLine("Creating a response file for the 'bundle' command...");

    Console.Write("Enter output file path (--output): ");
    var output = Console.ReadLine();

    Console.Write("Enter programming languages (--languages, comma-separated, e.g., 'cs,txt'): ");
    var languages = Console.ReadLine();

    Console.Write("Include source file paths as comments (--note)? (yes/no): ");
    var note = Console.ReadLine()?.ToLower() == "yes";

    Console.Write("Sort files by (--sort: name/type, default is 'name'): ");
    var sort = Console.ReadLine();

    Console.Write("Remove empty lines (--remove-empty-lines)? (yes/no): ");
    var removeEmptyLines = Console.ReadLine()?.ToLower() == "yes";

    Console.Write("Enter author name (--author, optional): ");
    var author = Console.ReadLine();

    var arguments = new List<string>
    {
        $"--output \"{output}\"",
        $"--languages \"{languages}\"",
        note ? "--note" : "",
        !string.IsNullOrWhiteSpace(sort) ? $"--sort {sort}" : "",
        removeEmptyLines ? "--remove-empty-lines" : "",
        !string.IsNullOrWhiteSpace(author) ? $"--author \"{author}\"" : ""
    }.Where(arg => !string.IsNullOrWhiteSpace(arg));

    var rspContent = $"bundle {string.Join(" ", arguments)}";
    File.WriteAllText(rspFile.FullName, rspContent);

    Console.WriteLine($"Response file created at: {rspFile.FullName}");
    Console.WriteLine($"Command in file: {rspContent}");
}, rspFileOption);

var rootCommand = new RootCommand("Root command for File bundler CLI");
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(createRspCommand);

await rootCommand.InvokeAsync(args);
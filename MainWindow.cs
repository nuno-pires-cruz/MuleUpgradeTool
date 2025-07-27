using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json; // Add this using directive

namespace WinFormsApp1
{
    public partial class MainWindow : Form
    {
        private TableLayoutPanel tableLayoutPanel;
        private SplitContainer splitContainer;
        private TreeView treeViewFolders;
        private ListBox listBoxFiles;
        private Button button;
        private FlowLayoutPanel flowPanel;
        private TextBox textBoxInfo;
        private GroupBox groupBox;
        private ToolTip toolTip;

        private Dictionary<string, string> replaceProperties;
        private Dictionary<string, string> replaceDependencies;
        private Dictionary<string, string> deleteDependencies;
        private Dictionary<string, string> replaceRepositories;
        private Dictionary<string, string> replaceDataWeaveExpressions;
        private Dictionary<string, string> replacePolicies;

        public MainWindow()
        {
            InitializeComponent();
            InitializeCustomComponents();

            // Load configuration from file
            var config = LoadConfig("config.json");
            replaceProperties = config.GetReplaceProperties();
            replaceDependencies = config.GetReplaceDependencies();
            deleteDependencies = config.GetDeleteDependencies();
            replaceRepositories = config.GetReplaceRepositories();
            replaceDataWeaveExpressions = config.GetReplaceDependencies();
            replacePolicies = config.GetReplacePolicies();
        }

        private void InitializeCustomComponents()
        {
            // TableLayoutPanel with 2 columns and 2 rows
            tableLayoutPanel = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 2,
                Dock = DockStyle.Fill
            };
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F)); // Main area
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F)); // Info area
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Main area
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // Bottom controls
            this.Controls.Add(tableLayoutPanel);

            // SplitContainer for TreeView and ListBox (main area, left column)
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 250,
                Orientation = Orientation.Vertical
            };
            tableLayoutPanel.Controls.Add(splitContainer, 0, 0);

            // TreeView for folders (left panel)
            treeViewFolders = new TreeView
            {
                Dock = DockStyle.Fill
            };
            treeViewFolders.AfterSelect += TreeViewFolders_AfterSelect;
            splitContainer.Panel1.Controls.Add(treeViewFolders);

            // ListBox for files (right panel)
            listBoxFiles = new ListBox
            {
                Dock = DockStyle.Fill
            };
            splitContainer.Panel2.Controls.Add(listBoxFiles);

            // TextBox for info (main area, right column)
            textBoxInfo = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            tableLayoutPanel.Controls.Add(textBoxInfo, 1, 0);

            // FlowLayoutPanel for bottom controls (spans both columns)
            flowPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Fill,
                Height = 50,
                Padding = new Padding(10, 10, 10, 10)
            };
            tableLayoutPanel.SetColumnSpan(flowPanel, 2);
            tableLayoutPanel.Controls.Add(flowPanel, 0, 1);

            // Replace button
            button = new Button
            {
                Text = "Main Changes",
                AutoSize = true
            };
            button.Click += ButtonMainChanges_Click;

            toolTip = new ToolTip();
            toolTip.SetToolTip(button, "POM \r\nBuild Path\r\nMule Artifact\r\n\r\nPosition on the POM file folder");

            flowPanel.Controls.Add(button);

            button = new Button
            {
                Text = "Policies Changes",
                AutoSize = true
            };
            button.Click += ButtonChangePolicies_Click;

            toolTip.SetToolTip(button, "Policies \r\n\r\nPosition on the Policies folder");

            flowPanel.Controls.Add(button);

            button = new Button
            {
                Text = "Dataweave Changes",
                AutoSize = true
            };
            button.Click += ButtonChangeDataweave_Click;

            toolTip.SetToolTip(button, "Datweave \r\n\r\nPosition on the src/main/mule folder");

            flowPanel.Controls.Add(button);

            button = new Button
            {
                Text = "Clear Log",
                AutoSize = true
            };
            button.Click += ButtonClearLog_Click;
            flowPanel.Controls.Add(button);

            // Load drives
            LoadDrives();
        }

        private void LoadDrives()
        {
            treeViewFolders.Nodes.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var node = new TreeNode(drive.Name) { Tag = drive.Name };
                node.Nodes.Add("..."); // Dummy node
                treeViewFolders.Nodes.Add(node);
            }
            treeViewFolders.BeforeExpand += TreeViewFolders_BeforeExpand;
        }

        private void TreeViewFolders_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "...")
            {
                e.Node.Nodes.Clear();
                try
                {
                    foreach (var dir in Directory.GetDirectories(e.Node.Tag!.ToString()!))
                    {
                        var dirNode = new TreeNode(Path.GetFileName(dir)) { Tag = dir };
                        dirNode.Nodes.Add("...");
                        e.Node.Nodes.Add(dirNode);
                    }
                }
                catch { /* Ignore access exceptions */ }
            }
        }

        private void TreeViewFolders_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            listBoxFiles.Items.Clear();
            try
            {
                var files = Directory.GetFiles(e.Node.Tag!.ToString()!);
                listBoxFiles.Items.AddRange(files);
            }
            catch { /* Ignore access exceptions */ }
        }

        private void ButtonMainChanges_Click(object? sender, EventArgs e)
        {
            var currentPath = treeViewFolders.SelectedNode.Tag?.ToString();

            var currentPathWithFile = currentPath + "/pom.xml";
            if (!File.Exists(currentPathWithFile))
            {
                MessageBox.Show("Position on the Folder where the POM is !");
                return;
            }

            try
            {
                // Load the XML file
                XDocument xmlDoc;

                using (var reader = new StreamReader(currentPathWithFile, Encoding.UTF8))
                {
                    xmlDoc = XDocument.Load(reader);
                }

                XNamespace defaultNs = xmlDoc.Root.GetDefaultNamespace();
                XElement? targetNode;

                textBoxInfo.AppendText("=================== POM ========================\r\n");

                textBoxInfo.AppendText("======================================properties\r\n");

                foreach (var replacement in replaceProperties)
                {
                    // Find the target node by name
                    targetNode = xmlDoc.Descendants()
                        .Where(e => e.Name.LocalName == replacement.Key)
                        .FirstOrDefault();
                    if (targetNode != null)
                    {
                        // Replace the value of the node
                        targetNode.Value = replacement.Value;
                        textBoxInfo.AppendText($"Node {replacement.Key} value updated successfully!\r\n");
                    }
                    else
                    {
                        textBoxInfo.AppendText($"Target {replacement.Key} node not found.\r\n");
                        targetNode = xmlDoc.Descendants()
                            .Where(e => e.Name.LocalName == "properties")
                            .FirstOrDefault();
                        if (targetNode != null)
                        {
                            var newElement = new XElement(defaultNs + replacement.Key, replacement.Value);
                            targetNode.Add(newElement);
                            textBoxInfo.AppendText($"properties {replacement.Key} added successfully!\r\n");
                        }
                        else
                        {
                            textBoxInfo.AppendText("properties not found.\r\n");
                        }
                    }
                }
                textBoxInfo.AppendText("======================================properties done\r\n");

                textBoxInfo.AppendText("======================================dependencies\r\n");

                foreach (var replacement in replaceDependencies)
                {
                    // Find the target dependency node by artifactId
                    targetNode = xmlDoc.Descendants()
                        .Where(e => e.Name.LocalName == "artifactId" && e.Value == replacement.Key)
                        .FirstOrDefault();
                    if (targetNode != null)
                    {
                        var newElement = XElement.Parse($"<dependency>{replacement.Value}</dependency>");
                        newElement.Name = defaultNs + "dependency"; // Ensure the namespace is set correctly
                        foreach (var element in newElement.Descendants())
                        {
                            element.Name = defaultNs + element.Name.LocalName;
                        }

                        targetNode.Parent.ReplaceWith(newElement);
                        textBoxInfo.AppendText($"Dependency {replacement.Key} updated successfully!\r\n");
                    }
                    else
                    {
                        textBoxInfo.AppendText($"Dependency {replacement.Key} not found.\r\n");
                    }
                }

                foreach (var replacement in deleteDependencies)
                {
                    // Find the target dependency node by artifactId
                    targetNode = xmlDoc.Descendants()
                        .Where(e => e.Name.LocalName == "artifactId" && e.Value == replacement.Key)
                        .FirstOrDefault();
                    if (targetNode != null)
                    {
                        targetNode.Parent.Remove();
                        textBoxInfo.AppendText($"Dependency {replacement.Key} removed successfully!\r\n");
                    }
                    else
                    {
                        textBoxInfo.AppendText($"Dependency {replacement.Key} not found.\r\n");
                    }
                }

                textBoxInfo.AppendText("======================================dependencies done\r\n");

                textBoxInfo.AppendText("======================================repositories\r\n");

                foreach (var replacement in replaceRepositories)
                {
                    // Find the target dependency node by artifactId
                    targetNode = xmlDoc.Descendants()
                        .Where(e => e.Name.LocalName == "id" && e.Value == replacement.Key)
                        .FirstOrDefault();
                    if (targetNode != null)
                    {
                        var newElement = XElement.Parse($"<repository>{replacement.Value}</repository>");
                        newElement.Name = defaultNs + "repository"; // Ensure the namespace is set correctly
                        foreach (var element in newElement.Descendants())
                        {
                            element.Name = defaultNs + element.Name.LocalName;
                        }

                        targetNode.Parent.ReplaceWith(newElement);
                        textBoxInfo.AppendText($"repository {replacement.Key} updated successfully!\r\n");
                    }
                    else
                    {
                        textBoxInfo.AppendText($"repository {replacement.Key} not found.\r\n");
                        targetNode = xmlDoc.Descendants()
                            .Where(e => e.Name.LocalName == "repositories")
                            .FirstOrDefault();
                        if (targetNode != null)
                        {
                            var newElement = XElement.Parse($"<repository>{replacement.Value}</repository>");
                            newElement.Name = defaultNs + "repository"; // Ensure the namespace is set correctly
                            foreach (var element in newElement.Descendants())
                            {
                                element.Name = defaultNs + element.Name.LocalName;
                            }

                            targetNode.Add(newElement);
                            textBoxInfo.AppendText($"repository {replacement.Key} added successfully!\r\n");
                        }
                        else
                        {
                            textBoxInfo.AppendText("repositories not found.\r\n");
                        }
                    }
                }

                textBoxInfo.AppendText("======================================repositories done\r\n");

                xmlDoc.Save(currentPathWithFile);

                textBoxInfo.AppendText("=================== POM done====================\r\n");

                textBoxInfo.AppendText("================ mule artifact ==================\r\n");

                currentPathWithFile = currentPath + "/mule-artifact.json";

                string content = "{\r\n  \"minMuleVersion\": \"4.9.0\",\r\n  \"javaSpecificationVersions\": [\"17\"]\r\n}";

                File.WriteAllText(currentPathWithFile, content); // Overwrites the file if it exists

                textBoxInfo.AppendText("mule-artifact contents replaced successfully!\r\n");

                textBoxInfo.AppendText("================ mule artifact done =============\r\n");

                textBoxInfo.AppendText("================ cleaning build path ============\r\n");

                try
                {
                    var currentPathWithFolder = currentPath + "/target";
                    if (Directory.Exists(currentPathWithFolder))
                    {
                        Directory.Delete(currentPathWithFolder, true);
                        textBoxInfo.AppendText($"Folder {currentPathWithFolder} and its contents deleted successfully.\r\n");
                    }
                    else
                    {
                        textBoxInfo.AppendText($"Folder {currentPathWithFolder} does not exist.\r\n");
                    }

                    currentPathWithFile = currentPath + "/.classpath";
                    if (File.Exists(currentPathWithFile))
                    {
                        File.Delete(currentPathWithFile);
                        textBoxInfo.AppendText($"File {currentPathWithFile} deleted successfully.\r\n");
                    }
                    else
                    {
                        textBoxInfo.AppendText($"File {currentPathWithFile} does not exist.\r\n");
                    }

                    currentPathWithFile = currentPath + "/.project";
                    if (File.Exists(currentPathWithFile))
                    {
                        File.Delete(currentPathWithFile);
                        textBoxInfo.AppendText($"File {currentPathWithFile} deleted successfully.\r\n");
                    }
                    else
                    {
                        textBoxInfo.AppendText($"File {currentPathWithFile} does not exist.\r\n");
                    }

                }
                catch (UnauthorizedAccessException)
                {
                    textBoxInfo.AppendText("Access denied. Unable to delete the folder.\r\n");
                }
                catch (IOException ex)
                {
                    textBoxInfo.AppendText($"An error occurred: {ex.Message}\r\n");
                }

                textBoxInfo.AppendText("================ cleaning build path done========\r\n");

                textBoxInfo.AppendText("+----------------------------------------------------\r\n");
                textBoxInfo.AppendText("|           main changes are done\r\n");
                textBoxInfo.AppendText("+----------------------------------------------------\r\n");
            }
            catch (Exception ex)
            {
                textBoxInfo.AppendText($"Error: {ex.Message}\r\n");
            }
        }

        private void ChangeJsonValueInFile(string filePath)
        {
            string policy = string.Empty;

            try
            {
                // Load the JSON file
                var json = File.ReadAllText(filePath);
                var jsonObject = Newtonsoft.Json.Linq.JObject.Parse(json);

                // Change the value of the specified key
                if (jsonObject.ContainsKey("assetId"))
                {
                    policy = jsonObject["assetId"]?.ToString() ?? "";
                    jsonObject["assetVersion"] = replacePolicies[policy];
                }
                // Save the changes back to the file
                File.WriteAllText(filePath, jsonObject.ToString(Newtonsoft.Json.Formatting.Indented));
                textBoxInfo.AppendText($"Key '{policy}' updated successfully!\r\n");
            }
            catch (Exception ex)
            {
                textBoxInfo.AppendText($"Error updating key '{policy}': {ex.Message}\r\n");
            }
        }

        private void ButtonChangePolicies_Click(object? sender, EventArgs e)
        {
            var currentPath = treeViewFolders.SelectedNode.Tag?.ToString();

            if (!currentPath.EndsWith("\\policies"))
            {
                MessageBox.Show("Position on the Folder where the policies are !");
                return;
            }

            textBoxInfo.AppendText("================ changing policies =============\r\n");

            try
            {
                foreach (string file in Directory.EnumerateFiles(treeViewFolders.SelectedNode.Tag?.ToString(), "*.json", SearchOption.AllDirectories))
                {
                        ChangeJsonValueInFile(file);
                }
            }
            catch (Exception ex)
            {
                textBoxInfo.AppendText($"Error: {ex.Message}\r\n");
            }

            textBoxInfo.AppendText("================ changing policies done ========\r\n");
        }

        private void ButtonClearLog_Click(object? sender, EventArgs e)
        {
            textBoxInfo.Clear();
        }

        // Inside your Form1 class, add a method to load the config:
        private Config LoadConfig(string configPath)
        {
            var json = File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
        }

        private void ButtonChangeDataweave_Click(object? sender, EventArgs e)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(treeViewFolders.SelectedNode.Tag?.ToString(), "*.xml", SearchOption.AllDirectories))
                {
                    foreach (var replacement in replaceDataWeaveExpressions)
                    {
                        if (FileReplaceText(file, replacement.Key, replacement.Value))
                        {
                            textBoxInfo.AppendText($"Replaced '{replacement.Key}' with '{replacement.Value}' in {file}\r\n");
                        }
                        else
                        {
                            textBoxInfo.AppendText($"'{replacement.Key}' not found in {file}\r\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                textBoxInfo.AppendText($"Error: {ex.Message}\r\n");
            }
        }

        static bool FileReplaceText(string filePath, string searchText, string replaceText)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                var found = content.Contains(searchText, StringComparison.OrdinalIgnoreCase);

                if (!found)
                {
                    return false; // Text not found, no replacement made
                }

                // Replace the text
                content = content.Replace(searchText, replaceText);

                // Write the updated content back to the file
                File.WriteAllText(filePath, content);

                return true; // Replacement successful
            }
            catch
            {
                // Handle files that can't be read (e.g., permissions issues)
                return false;
            }
        }
    }

    // Add this class to represent the configuration structure
    public class Config
    {
        public List<KeyValuePair<string, string>> ReplaceProperties {get;set; } = new();
        public List<KeyValuePair<string, string>> ReplaceDependencies { get; set; } = new();
        public List<KeyValuePair<string, string>> DeleteDependencies { get; set; } = new();
        public List<KeyValuePair<string, string>> ReplaceRepositories { get; set; } = new();
        public List<KeyValuePair<string, string>> ReplaceDataWeaveExpressions { get; set; } = new();
        public List<KeyValuePair<string, string>> ReplacePolicies { get; set; } = new();

        public Dictionary<string, string> GetReplaceProperties() {
            return ReplaceProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        public Dictionary<string, string> GetReplaceDependencies() {
            return ReplaceDependencies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        public Dictionary<string, string> GetDeleteDependencies() {
            return DeleteDependencies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        public Dictionary<string, string> GetReplaceRepositories() {
            return ReplaceRepositories.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        public Dictionary<string, string> GetReplaceDataWeaveExpressions() {
            return ReplaceDataWeaveExpressions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        public Dictionary<string, string> GetReplacePolicies() {
            return ReplacePolicies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
using System;
using System.IO;
using System.Diagnostics;
using ImGuiNET;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PlaywrightLang.LanguageServices;
using PlaywrightLang.LanguageServices.AST;
using PlaywrightLang.LanguageServices.Object;
using PlaywrightLang.LanguageServices.Object.Primitive;
using PlaywrightLang.LanguageServices.Parse;

namespace PlaywrightLang.DebugEntry;

internal class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private PwState state;
    private ImGuiRenderer _imGuiRenderer;
    private string _filePath = string.Empty;
    private string _statusMessage = string.Empty;
    private string _consoleOutput = string.Empty;
    private StringWriter _consoleWriter;
    private string _editorContent = string.Empty;
    private string _editorFileName = string.Empty;
    
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    
        // Create a writer that writes to both console and string
        var originalConsole = Console.Out;
        _consoleWriter = new StringWriter();
        Console.SetOut(new MultiWriter(originalConsole, _consoleWriter));
    }

    private class MultiWriter : TextWriter
    {
        private readonly TextWriter[] _writers;
    
        public MultiWriter(params TextWriter[] writers)
        {
            _writers = writers;
        }
    
        public override void Write(char value)
        {
            foreach (var writer in _writers)
                writer.Write(value);
        }
    
        public override void WriteLine(string value)
        {
            foreach (var writer in _writers)
                writer.WriteLine(value);
        }
    
        public override Encoding Encoding => Encoding.UTF8;
    }

    protected override void Initialize()
    {
        _imGuiRenderer = new ImGuiRenderer(this);
        _imGuiRenderer.RebuildFontAtlas();
        state = new PwState();
    
        // Set initial window size and position
        _graphics.PreferredBackBufferWidth = 1200;  // Increased width to accommodate both windows
        _graphics.PreferredBackBufferHeight = 800;
        _graphics.ApplyChanges();
    
        // Run tests.pw
        try
        {
            if (File.Exists("tests.pw"))
            {
                Console.WriteLine("Running tests.pw...");
                PwAst ast = state.ParseFile("tests.pw");
                state.ExecuteChunk(ast);
                _statusMessage = "Tests executed successfully!";
                _consoleOutput = _consoleWriter.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running tests: {ex.Message}");
            _statusMessage = $"Error running tests: {ex.Message}";
        }
    
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        
        base.Update(gameTime);
    }
    
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _imGuiRenderer.BeforeLayout(gameTime);
        
        // First window - Script Runner
        ImGui.Begin("Playwright Script Runner");
        
        ImGui.Text("Enter the script name (without .pw):");
        if (ImGui.InputText("##filename", ref _filePath, 256))
        {
            // Input text was modified
        }

        if (ImGui.Button("Run Script"))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_filePath))
                {
                    _statusMessage = "Please enter a script name";
                }
                else
                {
                    string scriptName = _filePath + ".pw";
                    string currentDirPath = scriptName;
                    string scriptsDirPath = Path.Combine("Scripts", scriptName);
            
                    string fullPath;
                    if (File.Exists(currentDirPath))
                    {
                        fullPath = currentDirPath;
                    }
                    else if (File.Exists(scriptsDirPath))
                    {
                        fullPath = scriptsDirPath;
                    }
                    else
                    {
                        _statusMessage = $"Script file not found in current directory or Scripts folder";
                        return;
                    }

                    _consoleWriter.GetStringBuilder().Clear();
                    var sw = new Stopwatch();
                    sw.Start();
                    PwAst ast = state.ParseFile(fullPath);
                    state.ExecuteChunk(ast);
                    sw.Stop();
                    _statusMessage = $"Script executed successfully in {sw.ElapsedMilliseconds}ms!";
                    _consoleOutput = _consoleWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error: {ex.Message}";
            }
        }

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            ImGui.TextColored(
                _statusMessage.StartsWith("Error:") ? new System.Numerics.Vector4(1, 0, 0, 1) : new System.Numerics.Vector4(0, 1, 0, 1), 
                _statusMessage);
        }
        
        if (!string.IsNullOrEmpty(_consoleOutput))
        {
            ImGui.Separator();
            ImGui.Text("Console Output:");
            if (ImGui.BeginChild("console", new System.Numerics.Vector2(0, 200), ImGuiChildFlags.None))  
            {
                ImGui.TextWrapped(_consoleOutput);
            }
            ImGui.EndChild();
        }
        
        ImGui.End();

        // Second window - Script Editor
        ImGui.Begin("Playwright Script Editor");
        
        ImGui.Text("File name (without .pw):");
        ImGui.InputText("##editorFileName", ref _editorFileName, 256);
        
        ImGui.Text("Script content:");
        ImGui.InputTextMultiline("##editor", ref _editorContent, 10000, new System.Numerics.Vector2(0, 300));

        if (ImGui.Button("Save Script"))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_editorFileName))
                {
                    _statusMessage = "Please enter a file name";
                }
                else
                {
                    string fileName = _editorFileName + ".pw";
                    string dirPath = "Scripts";
                    
                    // Create Scripts directory if it doesn't exist
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }
                    
                    string fullPath = Path.Combine(dirPath, fileName);
                    File.WriteAllText(fullPath, _editorContent);
                    _statusMessage = $"Script saved successfully to {fullPath}!";
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error saving file: {ex.Message}";
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Load Script"))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_editorFileName))
                {
                    _statusMessage = "Please enter a file name";
                }
                else
                {
                    string fileName = _editorFileName + ".pw";
                    string currentDirPath = fileName;
                    string scriptsDirPath = Path.Combine("Scripts", fileName);
                    
                    string fullPath;
                    if (File.Exists(currentDirPath))
                    {
                        fullPath = currentDirPath;
                    }
                    else if (File.Exists(scriptsDirPath))
                    {
                        fullPath = scriptsDirPath;
                    }
                    else
                    {
                        _statusMessage = $"Script file not found in current directory or Scripts folder";
                        return;
                    }

                    _editorContent = File.ReadAllText(fullPath);
                    _statusMessage = $"Script loaded successfully from {fullPath}!";
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error loading file: {ex.Message}";
            }
        }

        ImGui.End();
        
        _imGuiRenderer.AfterLayout();
        
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _imGuiRenderer?.Dispose();
        base.UnloadContent();
    }
}
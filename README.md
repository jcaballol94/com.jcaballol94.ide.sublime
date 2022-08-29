# com.jcaballol94.ide.sublime

This is an unofficial package to add support for Sublime Text as an IDE in Unity.

It generates a sublime-project file with the packages as selected in Preferences -> External Tools (simialr to the Visual Studio package).
The generated files are stored in the Library folder to avoid cluttering your project folder.

It also has support for Sublime's integrations of OmniSharp, both the [OmniSharp sublime package](https://github.com/OmniSharp/omnisharp-sublime) and my fork of the [LSP-OmniSharp sublime package](https://github.com/jcaballol94/LSP-OmniSharp).

## Installation

To add this package to your Unity project, go to the Package Manger, click on the + sign and select "Add package from git URL...".
See [Unity's documentation](https://docs.unity3d.com/Manual/upm-ui-giturl.html) for more details.

To start using it, simply go to Preferences -> External Tools and select Sublime Text. If it doesn't appear in the list, it means that the package was unable to find the Sublime Text installation.

## Finding Sublime's installation

* On Windows, the package will look for Sublime in the Program Files folder.
* On MacOS, it will first look in /Applications/Sublime Text and if it fails it will look for it in PATH.
* On Linux, it will look for Sublime in PATH

## Notes

If the project changes, for example adding or removing packages, it might be necessary to restart Sublime to see the changes.

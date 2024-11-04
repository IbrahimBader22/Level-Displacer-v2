using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace LevelDisplacer
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LevelDisplacerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Get UIApplication and UIDocument
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    message = "No active document found.";
                    return Result.Failed;
                }

                // Create the event handler and external event
                var eventHandler = new DisplaceLevelsEventHandler(uidoc.Document);
                var exEvent = ExternalEvent.Create(eventHandler);

                // Create and show the window
                var window = new LevelDisplacerWindow(uidoc, exEvent, eventHandler);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", $"An error occurred: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
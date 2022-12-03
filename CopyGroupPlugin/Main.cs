using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CopyGroupPlugin
{
    [Transaction(TransactionMode.Manual)]
    public class Main : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                Reference reference = uiDoc.Selection.PickObject(ObjectType.Element, new GroupFilter(), "Выберите группу элементов");
                Element element = doc.GetElement(reference);
                Group group = element as Group;
                XYZ groupCenter = GetElementCenter(group);
                Room initialRoom = GetRoomByPoint(doc, groupCenter);
                if (initialRoom == null)
                {
                    message = "Центральная точка группы находится за пределами помещения";
                    return Result.Failed;
                }

                XYZ initialRoomCenter = GetElementCenter(initialRoom);
                XYZ offset = groupCenter - initialRoomCenter;
                XYZ point = uiDoc.Selection.PickPoint("Выберите точку");
                Room finiteRoom = GetRoomByPoint(doc, point);
                if (finiteRoom == null)
                {
                    message = "Указанная точка находится вне области помещения";
                    return Result.Failed;
                }

                XYZ finiteRoomCenter = GetElementCenter(finiteRoom);
                XYZ finitePoint = offset + finiteRoomCenter;
                using (Transaction ts = new Transaction(doc, "Копирование группы"))
                {
                    ts.Start();
                    doc.Create.PlaceGroup(finitePoint, group.GroupType);
                    ts.Commit();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch(Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        public XYZ GetElementCenter(Element element)
        {
            BoundingBoxXYZ boundingBox = element.get_BoundingBox(null);
            XYZ centralPoint = (boundingBox.Max + boundingBox.Min) / 2;
            return centralPoint;
        }

        public Room GetRoomByPoint(Document document, XYZ point)
        {
            List<Room> listRoom = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area != 0)
                .ToList();

            if (listRoom.Any())
            {
                foreach (Room room in listRoom)
                {
                    if (room.IsPointInRoom(point))
                    {
                        return room;
                    }
                }
                return null;
            }
            else
            {
                return null;
            }
           
        }
    }
}

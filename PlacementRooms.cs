using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitAPITreningLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomatedPlacementOfRooms
{
    /*
     * Разработать плагин, позволяющий автоматизировать расстановку помещений и марок помещений в модели. 
     * Марка должна иметь формат "номерЭтажа_номерПомещения", например, 1_20 - первый этаж, 20-ое помещение.. 
     * Модель можно разработать самостоятельно или использовать готовую модель из предыдущих заданий.
     * 
     * В данном приложении используется семейство Room Tag_new
     */
    [Transaction(TransactionMode.Manual)]
    public class PlacementRooms : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> levelList = SelectionUtils.SelectAllElement<Level>(doc);

            if (levelList.Count < 1)
            {
                return Result.Failed;
            }


            //Проверка наличия маркировки помещений
            RoomTagType tagSymbol = (SelectionUtils.SelectAllElementCategoryType<RoomTagType>(doc, BuiltInCategory.OST_RoomTags))
                .Where(x => x.FamilyName.Equals("Room Tag_new")).FirstOrDefault();

            if (tagSymbol == null)
            {
                TaskDialog.Show("Ошибка", "Семейство \"Room Tag_new\" не найдено. Поэтому будут вставлены типовые марки помещений и нужный ответ не будет сформирован.");
            }
            else
            {
                using (Transaction ts = new Transaction(doc, "Активация семейства"))
                {
                    ts.Start();
                    if (!tagSymbol.IsActive)
                    {
                        tagSymbol.Activate();
                        doc.Regenerate();
                    }
                    ts.Commit();
                }
            }

            Parameter phasePram = doc.ActiveView.get_Parameter(BuiltInParameter.VIEW_PHASE);
            Phase phase = doc.GetElement(phasePram.AsElementId()) as Phase;
            if (phasePram.StorageType != StorageType.ElementId)
            {
                return Result.Failed;
            }

            List<Room> projectRooms = new List<Room>();
            for (int i = 0; i < levelList.Count; i++)
            {
                int indexLevel = i + 1; // Подвал и нижние этажы в нумирации не учитываются
                List<Room> listRoomLevel = InsertNewRoomInPlanCircuit(doc, levelList[i], phase, indexLevel.ToString());

                //Собираем общий список созданных помещений (для дальнейшей обработки)
                foreach (var room in listRoomLevel)
                {
                    projectRooms.Add(room);
                }
            }

            return Result.Succeeded;
        }


        List<Room> InsertNewRoomInPlanCircuit(Document doc, Level level, Phase constructionPhase, string nameRoomLevel)
        {
            List<Room> resRoom = new List<Room>();
            using (Transaction tr = new Transaction(doc, "Создание комнаты на плане"))
            {
                tr.Start();
                // Получение топологии на уровне
                PlanTopology planTopology = doc.get_PlanTopology(level);
                int i = 1;
                foreach (PlanCircuit circuit in planTopology.Circuits)
                {
                    //на наличие контура и отсутсвие в нем комнаты
                    if (circuit != null
                        && !circuit.IsRoomLocated)
                    {
                        //Создание не размещенного помещения (строчки в СП)
                        Room newScheduleRoom = doc.Create.NewRoom(constructionPhase);
                        newScheduleRoom.Name = "Помещение " + i.ToString();
                        //newScheduleRoom.Number = nameRoomLevel + "_" + i.ToString();

                        Room newRoom2 = null;
                        newRoom2 = doc.Create.NewRoom(newScheduleRoom, circuit);

                        if (null != newRoom2)
                        {
                            resRoom.Add(newRoom2);
                            //Запись в комментарий значения этажа
                            Parameter commentParam = newRoom2.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            commentParam.Set(nameRoomLevel);
                        }
                    }
                    i++;
                }
                tr.Commit();
            }

            return resRoom;
        }
    }

}

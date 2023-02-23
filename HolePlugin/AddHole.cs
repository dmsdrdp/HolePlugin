﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace HolePlugin
{
    [Transaction(TransactionMode.Manual)]

    public class AddHole : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document arDoc = commandData.Application.ActiveUIDocument.Document;
            Document ovDoc = arDoc.Application.Documents.OfType<Document>().Where(x => x.Title.Contains("ОВ")).FirstOrDefault();
            if (ovDoc == null)
            {
                TaskDialog.Show("Ошибка", "Не найден ОВ файл");
                return Result.Cancelled;
            }

            FamilySymbol familySymbol = new FilteredElementCollector(arDoc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .OfType<FamilySymbol>()
                .Where(x=>x.FamilyName.Equals("Отверстие"))
                .FirstOrDefault();

            if (familySymbol == null)
            {
                TaskDialog.Show("Ошибка", "Не найдено семейство \"Отверстие\"");
                return Result.Cancelled;
            }

            List<Duct> ducts = new FilteredElementCollector(ovDoc)              //Лист с воздуховодами
                .OfClass(typeof(Duct))
                .OfType<Duct>()
                .ToList();    
            
            List<Pipe> pipes = new FilteredElementCollector(ovDoc)              //Лист с трубами
                .OfClass(typeof(Pipe))
                .OfType<Pipe>()
                .ToList();                          

            View3D view3D = new FilteredElementCollector(arDoc)
                .OfClass(typeof(View3D))
                .OfType<View3D>()
                .Where(x => !x.IsTemplate)
                .FirstOrDefault();

            if (view3D == null)
            {
                TaskDialog.Show("Ошибка", "Не найден 3D вид");
                return Result.Cancelled;
            }

            ReferenceIntersector referenceIntersector = new ReferenceIntersector(new ElementClassFilter(typeof(Wall)), FindReferenceTarget.Element, view3D);

            Transaction transaction0 = new Transaction(arDoc);
            transaction0.Start("Расстаноовка отверстий");
                if (!familySymbol.IsActive)
            {
                familySymbol.Activate();
            }
                transaction0.Commit();  

            Transaction transaction = new Transaction(arDoc);
            transaction.Start("Расстановка отверстий");

            foreach(Duct d in ducts)                    //отверстия для воздуховодов
            {
                Line curve = (d.Location as LocationCurve).Curve as Line;
                XYZ point = curve.GetEndPoint(0);
                XYZ direction = curve.Direction;
                List<ReferenceWithContext> intersections = referenceIntersector.Find(point, direction)
                    .Where(x => x.Proximity <= curve.Length)
                    .Distinct(new ReferenceWithContextElementEqualityComparer())
                    .ToList();

                foreach(ReferenceWithContext refer in intersections)
                {
                    double proximity = refer.Proximity;
                    Reference reference = refer.GetReference();
                    Wall wall = arDoc.GetElement(reference.ElementId) as Wall;
                    Level level = arDoc.GetElement(wall.LevelId) as Level;
                    XYZ pointHole = point + (direction * proximity);

                    FamilyInstance hole = arDoc.Create.NewFamilyInstance(pointHole,familySymbol, wall, level, StructuralType.NonStructural);
                    Parameter width = hole.LookupParameter("Ширина");
                    Parameter height = hole.LookupParameter("Высота");
                    width.Set(d.Width);
                    height.Set(d.Height);
                }
            }
            foreach(Pipe d in pipes)                    //отверстия для труб
            {
                Line curve = (d.Location as LocationCurve).Curve as Line;
                XYZ point = curve.GetEndPoint(0);
                XYZ direction = curve.Direction;
                List<ReferenceWithContext> intersections = referenceIntersector.Find(point, direction)
                    .Where(x => x.Proximity <= curve.Length)
                    .Distinct(new ReferenceWithContextElementEqualityComparer())
                    .ToList();

                foreach (ReferenceWithContext refer in intersections)
                {
                    double proximity = refer.Proximity;
                    Reference reference = refer.GetReference();
                    Wall wall = arDoc.GetElement(reference.ElementId) as Wall;
                    Level level = arDoc.GetElement(wall.LevelId) as Level;
                    XYZ pointHole = point + (direction * proximity);

                    FamilyInstance hole = arDoc.Create.NewFamilyInstance(pointHole, familySymbol, wall, level, StructuralType.NonStructural);
                    Parameter width = hole.LookupParameter("Ширина");
                    Parameter height = hole.LookupParameter("Высота");
                    width.Set(d.Diameter);
                    height.Set(d.Diameter);
                }
            }
            transaction.Commit();
            return Result.Succeeded;
        }
    }
}

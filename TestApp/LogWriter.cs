using PriorityLock.Common.Interfaces;
using System;
using System.IO;
using System.Text;

namespace TestApp
{
    public class LogWriter : ILogger
    {
        private StringBuilder stringBuilder = new StringBuilder();

        public void WriteLine(string strToWrite)
        {
            Console.WriteLine(strToWrite);
            stringBuilder.AppendLine(strToWrite);
        }

        public void WriteLine()
        {
            Console.WriteLine();
            stringBuilder.AppendLine();
        }

        public void SaveToFile(string filePath)
        {
            File.WriteAllText(filePath, stringBuilder.ToString());
        }
    }
}

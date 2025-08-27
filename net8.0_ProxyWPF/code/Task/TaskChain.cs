namespace net8._0_ProxyWPF.code.Task
{
    public interface ITask<T>
    {
        int Priority { get; }
        string Name { get; }
        bool Execute(T context);
    }

    public class TaskComparer<T> : IComparer<ITask<T>>
    {
        private static int _id = 0;
        private readonly Dictionary<ITask<T>, int> _taskIds = new Dictionary<ITask<T>, int>();

        public int Compare(ITask<T> x, ITask<T> y)
        {
            if (x == null || y == null) return 0;
            int result = x.Priority.CompareTo(y.Priority);
            if (result != 0) return result;

            // 相同优先级时按唯一 ID 保证顺序
            if (!_taskIds.ContainsKey(x)) _taskIds[x] = ++_id;
            if (!_taskIds.ContainsKey(y)) _taskIds[y] = ++_id;
            return _taskIds[x].CompareTo(_taskIds[y]);
        }
    }

    public class DelegateTask<T> : ITask<T>
    {
        public int Priority { get; }
        public string Name { get; }
        private readonly Func<T, bool> _action;

        public DelegateTask(string name, int priority, Func<T, bool> action)
        {
            Name = name;
            Priority = priority;
            _action = action;
        }

        public bool Execute(T context) => _action(context);
    }

    public class TaskChain<T>
    {
        private readonly SortedSet<ITask<T>> _tasks;

        public TaskChain()
        {
            _tasks = new SortedSet<ITask<T>>(new TaskComparer<T>());
        }

        public void AddTask(string name, int priority, Func<T, bool> action)
        {
            _tasks.Add(new DelegateTask<T>(name, priority, action));
        }

        public void Execute(T context)
        {
            foreach (var task in _tasks)
            {
                if (!task.Execute(context))
                {
                    Console.WriteLine("任务链被中断！");
                    break;
                }
            }
        }
    }

}
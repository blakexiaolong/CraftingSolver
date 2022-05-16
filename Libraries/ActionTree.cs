namespace Libraries
{
    public class ActionNode
    {
        public Action? Action { get; set; }
        public State? State { get; set; }
        public List<ActionNode> Children { get; set; }
        public ActionNode? Parent { get; set; }

        public ActionNode(Action? action, State state, ActionNode parent)
        {
            Action = action;
            Children = new();
            Parent = parent;
            State = state;
        }

        public ActionNode Add(Action action, State state)
        {
            ActionNode node = new(action, state, this);
            Children.Add(node);
            return node;
        }

        public List<Action> GetPath()
        {
            List<Action> path = new();
            ActionNode head = this;
            while (head.Action != null && head.Parent != null)
            {
                path.Insert(0, head.Action);
                head = head.Parent;
            }
            return path;
        }
        public ActionNode? GetNode(IEnumerable<Action> path)
        {
            ActionNode head = this;
            foreach (Action action in path)
            {
                ActionNode? child = head.Children.FirstOrDefault(x => x.Action == action);
                if (child != null)
                {
                    head = child;
                }
                else
                {
                    return null;
                }
            }
            return head;
        }
    }
}

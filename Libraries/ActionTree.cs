namespace Libraries
{
    public class ActionNode
    {
        private Action? Action { get; set; }
        public LightState State { get; set; }
        private List<ActionNode> Children { get; set; }
        public ActionNode Parent { get; set; }

        public ActionNode(Action? action, LightState state, ActionNode parent)
        {
            Action = action;
            Children = new();
            Parent = parent;
            State = state;
        }

        public ActionNode Add(Action action, LightState state)
        {
            ActionNode node = new(action, state, this);
            Children.Add(node);
            return node;
        }

        public void Remove(ActionNode node)
        {
            Children.Remove(node);
        }

        public List<Action> GetPath(int length)
        {
            List<Action> path = new(length);
            ActionNode head = this;
            while (head.Action != null)
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

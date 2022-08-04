namespace Libraries
{
    public class ActionNode
    {
        public int? Action { get; set; }
        public LightState State { get; set; }
        private List<ActionNode> Children { get; set; }
        public ActionNode Parent { get; set; }

        public ActionNode(int? action, LightState state, ActionNode parent)
        {
            Action = action;
            Children = new();
            Parent = parent;
            State = state;
        }

        public ActionNode Add(int action, LightState state)
        {
            ActionNode node = new(action, state, this);
            Children.Add(node);
            return node;
        }

        public void Remove(ActionNode node)
        {
            Children.Remove(node);
        }

        public List<int> GetPath(int length)
        {
            List<int> path = new(length);
            ActionNode head = this;
            while (head.Action != null)
            {
                path.Insert(0, head.Action.Value);
                head = head.Parent;
            }
            return path;
        }
        public ActionNode? GetNode(IEnumerable<int> path)
        {
            ActionNode head = this;
            foreach (int action in path)
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

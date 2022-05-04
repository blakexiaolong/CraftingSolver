using System.Collections.Generic;

namespace CraftingSolver
{
    public class ActionNode
    {
        public Action Action { get; set; }
        public Dictionary<int, ActionNode> Children { get; set; }
        public ActionNode Parent { get; set; }
        public bool Failed { get; set; }

        public ActionNode(Action action, ActionNode parent)
        {
            Action = action;
            Children = new Dictionary<int, ActionNode>();
            Parent = parent;
            Failed = false;
        }
        public bool Add(Action action)
        {
            if (Children.ContainsKey(action.ID))
            {
                return !Children[action.ID].Failed;
            }
            else
            {
                Children.Add(action.ID, new ActionNode(action, this));
            }
            return true;
        }
        public bool Add(List<Action> actions)
        {
            ActionNode head = null;
            for (int i = 0; i < actions.Count; i++)
            {
                if (i == 0)
                {
                    if (Children.ContainsKey(actions[i].ID))
                    {
                        if (Children[actions[i].ID].Failed) return false;
                    }
                    else
                    {
                        Children.Add(actions[i].ID, new ActionNode(actions[i], head));
                    }
                    head = Children[actions[i].ID];
                }
                else if (i == actions.Count - 1)
                {
                    if (head.Children.ContainsKey(actions[i].ID))
                    {
                        return !head.Children[actions[i].ID].Failed;
                    }
                    else
                    {
                        head.Children.Add(actions[i].ID, new ActionNode(actions[i], head));
                    }
                }
                else
                {
                    if (head.Children.ContainsKey(actions[i].ID))
                    {
                        if (head.Children[actions[i].ID].Failed) return false;
                    }
                    else
                    {
                        head.Children.Add(actions[i].ID, new ActionNode(actions[i], head));
                    }
                    head = head.Children[actions[i].ID];
                }
            }
            return true;
        }
        public List<Action> GetPath()
        {
            List<Action> path = new List<Action>();
            ActionNode head = this;
            while (head.Parent != null)
            {
                path.Insert(0, head.Action);
                head = head.Parent;
            }
            return path;
        }
    }
}

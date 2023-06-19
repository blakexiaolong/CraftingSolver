using System.Numerics;

namespace Libraries;

public class BinaryTree
{
    public BinaryTree? Zero, One;

    public bool Create(BigInteger node)
    {
        bool found = true;
        BinaryTree head = this;
        int length = (int)node.GetBitLength();
        
        for (int i = 0; i < length; i++)
        {
            if (((node >> i) & 1) == 0)
            {
                if (head.Zero == null)
                {
                    head.Zero = new();
                    found = false;
                }
                head = head.Zero;
            }
            else
            {
                if (head.One == null)
                {
                    head.One = new();
                    found = false;
                }
                head = head.One;
            }
        }
        return !found;
    }
}
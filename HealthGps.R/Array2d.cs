using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HealthGps.R
{
    public sealed class Array2D<TValue> : IEquatable<Array2D<TValue>>
        where TValue : struct, IComparable, IComparable<TValue>, IEquatable<TValue>, IConvertible
    {
        private readonly TValue[] data;

        public Array2D() : this(0, 0)
        {
        }

        public Array2D(int nrows, int ncols)
        {
            Rows = nrows;
            Columns = ncols;
            data = new TValue[nrows * ncols];
        }

        public Array2D(int nrows, int ncols, TValue value)
            : this(nrows, ncols)
        {
            Fill(data, value);
        }

        public Array2D(int nrows, int ncols, TValue[,] values)
            : this(nrows, ncols)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            else if (values.Length > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(values));
            }

            //Copy data
            int index = 0;
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    data[index] = values[i, j];
                    index++;
                }
            }
        }

        public Array2D(int nrows, int ncols, TValue[] values)
            : this(nrows, ncols)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            else if (values.Length > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(values));
            }

            Array.Copy(values, data, values.Length);
        }

        public int Rows { get; }

        public int Columns { get; }

        public int Count => Rows * Columns;

        public TValue this[int row, int col]
        {
            get
            {
                CheckDimensions(row, col);
                return data[row * Columns + col];
            }

            set
            {
                CheckDimensions(row, col);
                data[row * Columns + col] = value;
            }
        }

        public void Clear()
        {
            Array.Clear(data, 0, data.Length);
        }

        public TValue[,] ToArray()
        {
            var result = new TValue[Rows, Columns];

            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    result[i, j] = this[i, j];
                }
            }

            return result;
        }

        public TValue[] ToArray1D()
        {
            var result = new TValue[data.Length];
            Array.Copy(data, result, data.Length);
            return result;
        }

        public override string ToString()
        {
            return ToString(null);
        }

        public string ToString(string format)
        {
            return ToString(format, null);
        }

        public string ToString(string format, IFormatProvider provider)
        {
            StringBuilder sb = new StringBuilder(Count * 8);
            sb.AppendFormat("Matrix({0}x{1}):", Rows, Columns);
            sb.AppendLine();
            if (format == null)
            {
                format = "G";
            }

            format = "{0:" + format + "}";
            string sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ";
            for (int i = 0; i < Rows; i++)
            {
                sb.Append("[");
                var listsep = string.Empty;
                for (int j = 0; j < Columns; j++)
                {
                    sb.Append(listsep);
                    sb.AppendFormat(provider, format, new object[] { this[i, j] });
                    listsep = sep;
                }

                sb.AppendLine("]");
            }

            return sb.ToString();
        }
        public override int GetHashCode()
        {
            return (Rows, Columns, data).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (!(obj is Array2D<TValue> other))
            {
                return false;
            }

            return Equals(other);
        }

        public bool Equals(Array2D<TValue> other)
        {
            if (other == null)
            {
                return false;
            }

            if (Rows != other.Rows || Columns != other.Columns)
            {
                return false;
            }

            return Enumerable.SequenceEqual(data, other.data);
        }

        public static bool operator ==(Array2D<TValue> left, Array2D<TValue> right)
        {
            if (((object)left) == null || ((object)right) == null)
                return Equals(left, right);

            return left.Equals(right);
        }

        public static bool operator !=(Array2D<TValue> left, Array2D<TValue> right)
        {
            if (((object)left) == null || ((object)right) == null)
                return !Object.Equals(left, right);

            return !(left.Equals(right));
        }

        public static void Fill<T>(T[] array, T value)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
        }

        private void CheckDimensions(int row, int column)
        {
            if ((row < 0) || (row >= Rows))
            {
                throw new IndexOutOfRangeException(
                    string.Format("Row {0} is out of array bounds.", row));
            }
            else if ((column < 0) || (column >= Columns))
            {
                throw new IndexOutOfRangeException(
                    string.Format("Column {0} is out of array bounds.", column));
            }
        }
    }
}

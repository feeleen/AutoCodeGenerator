/*
The MIT License (MIT)

Copyright (c) 2007 Roger Hill

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files 
(the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, 
publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do 
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE 
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN 
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

using DAL.SqlMetadata;
using DAL;

namespace AutoCodeGenLibrary
{
    public class CodeGenerator : CodeGeneratorBase, IGenerator
    {
        /// <summary>
        /// This object is a collection of characters that could create problems if they are used in c# variable names. 
        /// This object is a 'hit list' of characters to remove.
        /// </summary>
        private readonly static string[] CSHARP_UNDESIREABLES = new string[] { "!", "$", "%", "^", "*", "(", ")", "-", "+", "=", "{", "}", "[", "]", ":", ";", "|", "'", "<", ">", ",", ".", "?", "/", " ", "~", "`", "\"", "\\" };
        private readonly static int[] PRIME_NUMBER_LIST = new int[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239, 241, 251, 257, 263, 269, 271 };
        private readonly static string SQL_PARAMETER_TEMPLATE = "parameters.Add(new SqlParameter() {{ ParameterName = \"{0}\", SqlDbType = SqlDbType.{1}, Size = {2}, Value = {3} }});";

        public static readonly string CONVERT_NULLABLE_FIELDS = "Convert Nullable Fields";
        public static readonly string INCLUDE_IS_DIRTY_FLAG = "Include Is Dirty Flag";

        public eLanguage Language
        {
            get { return eLanguage.Csharp; }
        }
        public eCategory Category
        {
            get { return eCategory.MiddleTier; }
        }
        public Dictionary<string, string> Methods
        {
            get
            {
                return new Dictionary<string, string>()
                {
                    { "C# Orm Class", "GenerateCSharpOrmClass" },
                    { "C# External Orm Class", "GenerateCSharpExternalOrmClass" },
                    { "C# Dal Class", "GenerateCSharpDalClass" },
                    { "C# External Dal Class", "GenerateCSharpExternalDalClass" },
                    { "C# Class Interface", "GenerateCSharpClassInterface" },
                    { "C# Enumeration", "GenerateCSharpEnumeration" },
                    { "C# Base Class", "GenerateCSharpBaseClass" },
                    { "Xml Loader", "GenerateXmlLoader" },
                };
            }
        }
        public Dictionary<string, bool> Options
        {
            get
            {
                return new Dictionary<string, bool>()
                {
                    { CONVERT_NULLABLE_FIELDS, false},
                    { INCLUDE_IS_DIRTY_FLAG, false},
                };
            }
        }

        public CodeGenerator() { }

        public OutputObject GenerateCSharpOrmClass(SqlTable sqlTable, List<string> namespaceIncludes, bool includeClassDecoration)
        {
            if (sqlTable == null)
                return null;

            //todo: wrire these up somewhere
            bool include_region_blocks = false;
            bool include_static_field_names = false;
            bool include_to_string_overload = false;
            bool include_equals_overload = false;
            bool include_get_hash_overload = false;
            bool include_reset_class_method = false;

            string class_name = NameFormatter.ToCSharpClassName(sqlTable.Name);

            var output = new OutputObject();
            output.Name = class_name + ".cs";
            output.Type = OutputObject.eObjectType.CSharp;

            var sb = new StringBuilder();

            #region Header block

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine();

            // not needed here
            //sb.AppendLine(GenerateNamespaceIncludes(namespace_includes, null));

            #endregion

            sb.AppendLine("namespace " + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name));
            sb.AppendLine("{");

            //if (includeClassDecoration)
            //    sb.AppendLine(AddTabs(1) + "[SQLTableAttribute(DataBaseName=\"" + sqlTable.Database.Name + "\", TableName=\"" + sqlTable.Name + "\")]");

            sb.AppendLine(AddTabs(1) + "public partial class " + class_name);
            sb.AppendLine(AddTabs(1) + "{");

            #region Constants Bloc
            ////////////////////////////////////////////////////////////////////////////////

            #region sample output
            //#region Constants
            //
            //    public static class Db
            //    {
            //        public static string Id             = "Id";
            //    }
            //
            //#endregion
            #endregion

            if (include_static_field_names)
            {
                if (include_region_blocks)
                {
                    sb.AppendLine(AddTabs(2) + "#region Constants");
                    sb.AppendLine();
                }
                sb.AppendLine(AddTabs(2) + "public static class Db");
                sb.AppendLine(AddTabs(2) + "{");

                foreach (var sql_column in sqlTable.Columns.Values)
                {
                    // format: 
                    // public static string Id = "Id";
                    sb.AppendLine(AddTabs(3) + "public static string " + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "= \"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\";");
                }

                sb.AppendLine(AddTabs(2) + "}");
                sb.AppendLine();

                if (include_region_blocks)
                {
                    sb.AppendLine(AddTabs(2) + "#endregion");
                    sb.AppendLine();
                }
            }

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region Properties Block
            ////////////////////////////////////////////////////////////////////////////////

            if (include_region_blocks)
            {
                sb.AppendLine(AddTabs(2) + "#region Properties");
                sb.AppendLine();
            }

            foreach (var sql_column in sqlTable.Columns.Values)
            {
                #region Sample Output
                //[SQLColumn(ColumnName = "foo", SQLType = "int", Precision = "4", IsPrimaryKey = "false", IsNullable = "false")]
                //public string SomeID { get; set; }
                //{
                //  get { return _SomeID; }
                //  set { _SomeID = value; }
                //}
                #endregion Sample Output

                if (includeClassDecoration)
                {
                    sb.Append(AddTabs(2) + "[SQLColumnAttribute(");
                    sb.Append("ColumnName=\"" + sql_column.Name + "\", ");
                    sb.Append("SQLType=SqlDbType." + sql_column.DataType + ", ");
                    sb.Append("Length=" + sql_column.Length.ToString() + ", ");
                    sb.Append("IsPrimaryKey=" + sql_column.IsPk.ToString().ToLower() + ", ");
                    sb.Append("IsNullable=" + sql_column.IsNullable.ToString().ToLower());
                    sb.Append(")]" + Environment.NewLine);
                }

                sb.AppendLine(AddTabs(2) + $"public {NameFormatter.SQLTypeToCSharpType(sql_column)} {NameFormatter.ToCSharpPropertyName(sql_column.Name)} {{ get; set; }}");
            }

            if (include_region_blocks)
            {
                sb.AppendLine(AddTabs(2) + "#endregion");
                sb.AppendLine();
            }

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            if (include_region_blocks)
            {
                sb.AppendLine();
                sb.AppendLine(AddTabs(2) + "#region Methods");
            }

            #region Default CTOR
            ////////////////////////////////////////////////////////////////////////////////

            #region sample output
            //public Foo() {}
            #endregion

            sb.AppendLine();
            sb.AppendLine(AddTabs(2) + "public " + NameFormatter.ToCSharpClassName(sqlTable.Name) + "() {}");

            ////////////////////////////////////////////////////////////////////////////////
            #endregion Default CTOR

            #region Reset Method
            //////////////////////////////////////////////////////////////////////////////

            if (include_reset_class_method)
            {
                sb.AppendLine();
                sb.AppendLine(AddTabs(2) + "public void Reset()");
                sb.AppendLine(AddTabs(2) + "{");

                foreach (var sql_column in sqlTable.Columns.Values)
                {
                    // format: 
                    // _State   = string.Empty;
                    sb.Append(AddTabs(3) + NameFormatter.ToCSharpPrivateVariable(sql_column.Name) + " = " + NameFormatter.GetCSharpDefaultValue(sql_column) + ";" + Environment.NewLine);
                }

                sb.AppendLine(AddTabs(2) + "}");
            }

            //////////////////////////////////////////////////////////////////////////////
            #endregion

            #region ToString Method
            ////////////////////////////////////////////////////////////////////////////////

            if (include_to_string_overload)
            {
                #region Sample Output
                //public override string ToString()
                //{
                //    var sb = new StringBuilder();
                //
                //    sb.AppendLine("CountryID:" + _CountryID.ToString());
                //    sb.AppendLine("OfficialName:" + _OfficialName);
                //    sb.AppendLine("CommonName:" + _CommonName);
                //    sb.AppendLine("CapitolName:" + _CapitolName);
                //    sb.AppendLine("Disabled:" + _Disabled.ToString());
                //
                //    return sb.ToString();
                //}
                #endregion

                sb.AppendLine();
                sb.AppendLine(AddTabs(2) + "public override string ToString()");
                sb.AppendLine(AddTabs(2) + "{");
                sb.AppendLine(AddTabs(3) + "var sb = new StringBuilder();");
                sb.AppendLine();

                foreach (var sql_column in sqlTable.Columns.Values)
                {
                    // format: 
                    // sb.AppendLine("Foo:" + _Foo.ToString());
                    if (NameFormatter.SQLTypeToCSharpType(sql_column) == "string")
                    {
                        sb.AppendLine(AddTabs(3) + "sb.AppendLine(\"" + sql_column.Name + ": \" + " + NameFormatter.ToCSharpPrivateVariable(sql_column.Name) + " + \" \");");
                    }
                    else
                    {
                        sb.AppendLine(AddTabs(3) + "sb.AppendLine(\"" + sql_column.Name + ": \" + " + NameFormatter.ToCSharpPrivateVariable(sql_column.Name) + ".ToString() + \" \");");
                    }
                }

                sb.AppendLine();
                sb.AppendLine(AddTabs(3) + "return sb.ToString();");
                sb.AppendLine(AddTabs(2) + "}");
            }

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region Equals Method
            ////////////////////////////////////////////////////////////////////////////////

            if (include_equals_overload)
            {
                #region Sample Output
                //public override bool Equals(object obj)
                //{
                //    if (ReferenceEquals(null, obj)) return false;
                //    if (ReferenceEquals(this, obj)) return true;
                //    if (GetType() != obj.GetType()) return false;
                //
                //    cArmor new_obj = (cArmor)obj;     
                //
                //    if (!Object.Equals(this._ArmorID, new_obj._ArmorID)) return false;
                //    if (!Object.Equals(this._ArmorType, new_obj._ArmorType)) return false;
                //    if (!Object.Equals(this._Disabled, new_obj._Disabled)) return false;
                //
                //    return true;
                //}
                #endregion

                sb.AppendLine();
                sb.AppendLine(AddTabs(2) + "public override bool Equals(object obj)");
                sb.AppendLine(AddTabs(2) + "{");
                sb.AppendLine(AddTabs(3) + "if (ReferenceEquals(null, obj)) return false;");
                sb.AppendLine(AddTabs(3) + "if (ReferenceEquals(this, obj)) return true;");
                sb.AppendLine(AddTabs(4) + "if (GetType() != obj.GetType()) return false;");
                sb.AppendLine();

                sb.AppendLine(AddTabs(3) + NameFormatter.ToCSharpClassName(sqlTable.Name) + " new_obj = (" + NameFormatter.ToCSharpClassName(sqlTable.Name) + ")obj;");
                sb.AppendLine();

                foreach (var sql_column in sqlTable.Columns.Values)
                {
                    // format: 
                    // if (!Object.Equals(this._ArmorID, new_obj._ArmorID)) return false;
                    sb.AppendLine(AddTabs(3) + "if (!object.Equals(this." + NameFormatter.ToCSharpPrivateVariable(sql_column.Name) + ", new_obj." + NameFormatter.ToCSharpPrivateVariable(sql_column.Name) + ")) return false;");
                }

                sb.AppendLine();
                sb.AppendLine(AddTabs(3) + "return true;");
                sb.AppendLine(AddTabs(2) + "}");
            }

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region GetHashCode Method
            ////////////////////////////////////////////////////////////////////////////////

            if (include_get_hash_overload)
            {
                #region Sample Output
                //public override int GetHashCode()
                //{
                //    int output = 0;
                //
                //    output ^= (_ArmorID * 2);
                //    output ^= (_ArmorType.GetHashCode() * 3);
                //    output ^= (_Disabled.GetHashCode() * 5);
                //
                //    return output;
                //}
                #endregion

                sb.AppendLine();
                sb.AppendLine(AddTabs(2) + "public override int GetHashCode()");
                sb.AppendLine(AddTabs(2) + "{");
                sb.AppendLine(AddTabs(3) + "unchecked");
                sb.AppendLine(AddTabs(3) + "{");
                sb.AppendLine(AddTabs(4) + "int output = 0;");
                sb.AppendLine();

                int prime = 0;

                foreach (var sql_column in sqlTable.Columns.Values)
                {
                    // format: 
                    // output ^= _ArmorType.GetHashCode();
                    switch (sql_column.SqlDataType)
                    {
                        case SqlDbType.TinyInt:
                        case SqlDbType.SmallInt:
                        case SqlDbType.Int:
                        case SqlDbType.BigInt:
                            sb.AppendLine(AddTabs(4) + $"output ^= ({NameFormatter.ToCSharpPrivateVariable(sql_column.Name)} * {PRIME_NUMBER_LIST[prime]});");
                            break;

                        default:
                            sb.AppendLine(AddTabs(4) + $"output ^= ({NameFormatter.ToCSharpPrivateVariable(sql_column.Name)}.GetHashCode() * {PRIME_NUMBER_LIST[prime]});");
                            break;
                    }

                    prime++;

                    // my what a wide table you have...
                    if (prime > PRIME_NUMBER_LIST.Length)
                        prime = 0;
                }

                sb.AppendLine();
                sb.AppendLine(AddTabs(4) + "return output;");
                sb.AppendLine(AddTabs(3) + "}");
                sb.AppendLine(AddTabs(2) + "}");
            }

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            if (include_region_blocks)
            {
                sb.AppendLine();
                sb.AppendLine(AddTabs(2) + "#endregion");
            }

            sb.AppendLine(AddTabs(1) + "}");
            sb.AppendLine("}");

            output.Body = sb.ToString();
            return output;
        }

        public OutputObject GenerateCSharpExternalOrmClass(SqlTable sqlTable, List<string> namespaceIncludes)
        {
            if (sqlTable == null)
                return null;

            string class_name = NameFormatter.ToCSharpClassName(sqlTable.Name);

            OutputObject output = new OutputObject();
            output.Name = class_name + "_Ext.cs";
            output.Type = OutputObject.eObjectType.CSharp;

            var sb = new StringBuilder();

            #region Header block

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine();

            // not needed here
            //sb.AppendLine(GenerateNamespaceIncludes(namespace_includes, null));

            #endregion

            sb.AppendLine("namespace " + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name));
            sb.AppendLine("{");
            sb.AppendLine(AddTabs(1) + "public partial class " + class_name);
            sb.AppendLine(AddTabs(1) + "{");
            sb.AppendLine();
            sb.AppendLine(AddTabs(1) + "}");
            sb.AppendLine("}");

            output.Body = sb.ToString();
            return output;
        }

        public OutputObject GenerateCSharpDalClass(SqlTable sqlTable, List<string> namespaceIncludes, bool convertNullableFields, bool includeIsDirtyFlag)
        {
            //public class FooClass
            //{
            //    private List<FooDataStrure> _Foo = null;
            //
            //    //public FooClass()
            //    //{ 
            //    //    // foo
            //    //}
            //
            //    public List<FooDataStrure> LoadData(DataTable dt)
            //    {
            //        _Foo.Clear();  
            //        foreach (DataRow dr in dt.Rows)
            //        {
            //            p_Foo.Add(new FooDataStrure(Convert.ToInt32(dr["whatever"])));
            //        }
            //
            //        return _Foo;
            //    }
            //}

            if (sqlTable == null)
                throw new ArgumentException("Sql table cannot be null or empty");

            string list_type = NameFormatter.ToCSharpClassName(sqlTable.Name);
            string sql_tablename = NameFormatter.ToTSQLName(sqlTable.Name);
            string collection_type = $"List<{list_type}>";
            string class_name = NameFormatter.ToCSharpClassName("Dal" + sqlTable.Name);
            SqlColumn id_column = sqlTable.GetIdColumn();

            // todo filter this for list results?
            if (id_column == null)
                throw new ArgumentException("Sql table needs an identity or primary key");

            OutputObject output = new OutputObject();
            output.Name = class_name + ".cs";
            output.Type = OutputObject.eObjectType.CSharp;

            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using System.Data.SqlClient;");
            sb.AppendLine();

            sb.AppendLine("using DAL;");
            sb.AppendLine(GenerateNamespaceIncludes(namespaceIncludes));

            sb.AppendLine("namespace " + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name));
            sb.AppendLine("{");
            //sb.AppendLine(AddTabs(1) + "[Serializable]");
            sb.AppendLine(AddTabs(1) + "public partial class " + class_name);
            sb.AppendLine(AddTabs(1) + "{");

            ////////////////////////////////////////////////////////////////////////////////

            sb.AppendLine(AddTabs(2) + "protected string _SQLConnection;");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////

            #region Default CTOR
            ////////////////////////////////////////////////////////////////////////////////

            //public Foo()
            //{
            //    //foo
            //}

            sb.AppendLine(AddTabs(2) + $"public {class_name}(string sqlConn)");
            sb.AppendLine(AddTabs(2) + "{");
            sb.AppendLine(AddTabs(3) + "_SQLConnection = sqlConn;");
            sb.AppendLine(AddTabs(2) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region LoadAllFromDb
            ////////////////////////////////////////////////////////////////////////////////

            #region Code Sample
            //public List<cGame> LoadAllFromDb()
            //{
            //    return Database.ExecuteQuerySp<cGame>("[GalacticConquest].[dbo].[up_Game_SelectAll]", null, _SQLConnection);
            //}
            #endregion

            sb.AppendLine(AddTabs(2) + $"public {collection_type} LoadAllFromDb()");
            sb.AppendLine(AddTabs(2) + "{");
            sb.AppendLine(AddTabs(3) + $"return Database.ExecuteQuerySp<{list_type}>(\"[{NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name)}].[{sqlTable.Schema}].[" + NameFormatter.GenerateSqlStoredProcName(sqlTable.Name, eStoredProcType.SelectAll, null) + "]\", null, _SQLConnection);");
            sb.AppendLine(AddTabs(2) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region LoadListFromDb
            ////////////////////////////////////////////////////////////////////////////////

            #region Code Sample
            //public List<cGame> LoadListFromDb(IEnumerable<int> id_list)
            //{
            //    List<SqlParameter> parameters = new List<SqlParameter>();
            //    parameters.Add(new SqlParameter() { ParameterName = "@IdList", SqlDbType = SqlDbType.VarChar, Size = -1, Value = Database.GenericListToStringList(id_list) });
            //    return Database.ExecuteQuerySp<cGame>("[GalacticConquest].[dbo].[up_Game_SelectMany]", parameters, _SQLConnection);
            //}
            #endregion

            if (sqlTable.PkList.Count == 0)
            {
                sb.AppendLine("// Cannot create LoadListFromDb() without a pk on database table.");
            }
            else
            {
                sb.AppendLine(AddTabs(3) + "public " + collection_type + " LoadListFromDb(IEnumerable<int> id_list)");
                sb.AppendLine(AddTabs(3) + "{");

                sb.AppendLine(AddTabs(4) + "List<SqlParameter> parameters = new List<SqlParameter>();");
                sb.AppendLine(AddTabs(4) + string.Format(SQL_PARAMETER_TEMPLATE, "@IdList", "VarChar", -1, "Database.GenericListToStringList(id_list)"));
                sb.AppendLine(AddTabs(4) + "return Database.ExecuteQuerySp<" + list_type + ">(\"" + NameFormatter.ToTSQLName(sqlTable.Database.Name) + ".[dbo].[" + NameFormatter.GenerateSqlStoredProcName(sqlTable.Name, eStoredProcType.SelectMany, null) + "]\", parameters, _SQLConnection);");
                sb.AppendLine(AddTabs(3) + "}");
                sb.AppendLine();
            }

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region LoadAllFromDbPaged
            ////////////////////////////////////////////////////////////////////////////////

            #region Code Sample
            //public List<cGame> LoadAllFromDbPaged(int start_index, int page_size, string search_string)
            //{
            //    List<SqlParameter> parameters = new List<SqlParameter>();
            //    parameters.Add(new SqlParameter() { ParameterName = "@StartNumber", SqlDbType = SqlDbType.Int, Size = 4, Value = start_index });
            //    parameters.Add(new SqlParameter() { ParameterName = "@PageSize", SqlDbType = SqlDbType.Int, Size = 4, Value = page_size });
            //    parameters.Add(new SqlParameter() { ParameterName = "@SearchString", SqlDbType = SqlDbType.VarChar, Size = 50, Value = search_string });
            //    return Database.ExecuteQuerySp<cGame>("[GalacticConquest].[dbo].[up_Game_SelectAllPaged]", parameters, _SQLConnection);
            //}
            #endregion

            sb.AppendLine(AddTabs(3) + "public " + collection_type + " LoadAllFromDbPaged(int start_index, int page_size, string search_string)");
            sb.AppendLine(AddTabs(3) + "{");

            sb.AppendLine(AddTabs(4) + "List<SqlParameter> parameters = new List<SqlParameter>();");
            sb.AppendLine(AddTabs(4) + string.Format(SQL_PARAMETER_TEMPLATE, "@StartNumber", "Int", 4, "start_index"));
            sb.AppendLine(AddTabs(4) + string.Format(SQL_PARAMETER_TEMPLATE, "@PageSize", "Int", 4, "page_size"));
            sb.AppendLine(AddTabs(4) + string.Format(SQL_PARAMETER_TEMPLATE, "@SearchString", "VarChar", 50, "search_string"));
            sb.AppendLine(AddTabs(4) + $"return Database.ExecuteQuerySp<{list_type}>(\"[{NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name)}].[{sqlTable.Schema}].[{NameFormatter.GenerateSqlStoredProcName(sqlTable.Name, eStoredProcType.SearchPaged, null)}]\", parameters, _SQLConnection);");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region LoadSingleFromDb
            ////////////////////////////////////////////////////////////////////////////////

            #region Code Sample
            //public cGame LoadSingleFromDb(int id)
            //{
            //    List<SqlParameter> parameters = new List<SqlParameter>();
            //    parameters.Add(new SqlParameter() { ParameterName = "@Id", SqlDbType = SqlDbType.Int, Size = 4, Value = id });
            //    var results = Database.ExecuteQuerySp<cGame>("[GalacticConquest].[dbo].[up_Game_SelectSingle]", parameters, _SQLConnection);

            //    if (results != null && results.Count > 0)
            //        return results[0];
            //    else
            //        return null;
            //}
            #endregion

            sb.AppendLine(AddTabs(3) + "public " + list_type + " LoadSingleFromDb(" + NameFormatter.GenerateCSharpFunctionArgs(sqlTable, eIncludedFields.PKOnly) + ")");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "List<SqlParameter> parameters = new List<SqlParameter>();");

            // generate all Sp parameters
            foreach (var sql_column in sqlTable.Columns.Values)
            {
                if (sql_column.IsPk)
                    sb.AppendLine(AddTabs(4) + string.Format(SQL_PARAMETER_TEMPLATE, NameFormatter.ToTSQLVariableName(sql_column), sql_column.SqlDataType, sql_column.Length, NameFormatter.ToCSharpLocalVariable(sql_column.Name)));
            }

            sb.AppendLine(AddTabs(4) + "var results = Database.ExecuteQuerySp<" + list_type + ">(\"[" + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name) + "].[dbo].[" + NameFormatter.GenerateSqlStoredProcName(sqlTable.Name, eStoredProcType.SelectSingle, null) + "]\", parameters, _SQLConnection);");
            sb.AppendLine();

            sb.AppendLine(AddTabs(4) + "if (results != null && results.Count > 0)");
            sb.AppendLine(AddTabs(5) + "return results[0];");
            sb.AppendLine(AddTabs(4) + "else");
            sb.AppendLine(AddTabs(5) + "return null;");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region DeleteSingleFromDb
            ////////////////////////////////////////////////////////////////////////////////

            #region Code Sample
            //public bool DeleteSingleFromDb(int id)
            //{
            //    List<SqlParameter> parameters = new List<SqlParameter>();
            //    parameters.Add(new SqlParameter() { ParameterName = "@Id", SqlDbType = SqlDbType.Int, Size = 4, Value = id });
            //    return Database.ExecuteNonQuerySp("[GalacticConquest].[dbo].[up_Game_DeleteSingle]", parameters, _SQLConnection) > 0;
            //}
            #endregion

            sb.AppendLine(AddTabs(3) + "public bool DeleteSingleFromDb(" + NameFormatter.GenerateCSharpFunctionArgs(sqlTable, eIncludedFields.PKOnly) + ")");
            sb.AppendLine(AddTabs(3) + "{");

            sb.AppendLine(AddTabs(4) + "List<SqlParameter> parameters = new List<SqlParameter>();");

            // generate all SP parameters
            foreach (var sql_column in sqlTable.Columns.Values)
            {
                if (sql_column.IsPk)
                    sb.AppendLine(AddTabs(4) + string.Format(SQL_PARAMETER_TEMPLATE, NameFormatter.ToTSQLVariableName(sql_column), sql_column.SqlDataType, sql_column.Length, NameFormatter.ToCSharpLocalVariable(sql_column.Name)));
            }

            sb.AppendLine(AddTabs(4) + "return Database.ExecuteNonQuerySp(\"" + NameFormatter.ToTSQLName(sqlTable.Database.Name) + ".[dbo].[" + NameFormatter.GenerateSqlStoredProcName(sqlTable.Name, eStoredProcType.DelSingle, null) + "]\", parameters, _SQLConnection) > 0;");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region DeleteListFromDb
            ////////////////////////////////////////////////////////////////////////////////

            #region Code Sample
            //public int DeleteListFromDb(IEnumerable<int> id_list)
            //{
            //    List<SqlParameter> parameters = new List<SqlParameter>();
            //    parameters.Add(new SqlParameter() { ParameterName = "@IdList", SqlDbType = SqlDbType.VarChar, Size = -1, Value = Database.GenericListToStringList<int>(id_list) });
            //    return Database.ExecuteNonQuerySp("[GalacticConquest].[dbo].[up_Game_DeleteMany]", parameters, _SQLConnection);
            //}
            #endregion

            sb.AppendLine(AddTabs(3) + "public int DeleteListFromDb(IEnumerable<int> id_list)");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "List<SqlParameter> parameters = new List<SqlParameter>();");
            sb.AppendLine(AddTabs(4) + string.Format(SQL_PARAMETER_TEMPLATE, "@IdList", "VarChar", -1, "Database.GenericListToStringList(id_list)"));
            sb.AppendLine(AddTabs(4) + "return Database.ExecuteNonQuerySp(\"" + NameFormatter.ToTSQLName(sqlTable.Database.Name) + ".[dbo].[" + NameFormatter.GenerateSqlStoredProcName(sqlTable.Name, eStoredProcType.DelMany, null) + "]\", parameters, _SQLConnection);");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region DeleteAllFromDb
            ////////////////////////////////////////////////////////////////////////////////

            //public int DeleteAllFromDb()
            //{
            //    return Database.ExecuteNonQuerySp("[GalacticConquest].[dbo].[up_Game_DeleteAll]", null, _SQLConnection);
            //}

            sb.AppendLine(AddTabs(3) + "public int DeleteAllFromDb()");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "return Database.ExecuteNonQuerySp(\"" + NameFormatter.ToTSQLName(sqlTable.Database.Name) + ".[dbo].[" + NameFormatter.GenerateSqlStoredProcName(sqlTable.Name, eStoredProcType.DelAll, null) + "]\", null, _SQLConnection);");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region LoadFromXmlFile
            ////////////////////////////////////////////////////////////////////////////////

            //public bool LoadFromXmlFile(string filename)
            //{
            //    string file_data = FileIo.ReadFromFile(filename, out file_data);
            //    DataSet ds = XmlConverter.XmlStringToDataSet(file_data);
            //    return LoadFromDataTable(ds.Tables["Game"], out results, out exception);
            //}

            sb.AppendLine(AddTabs(3) + "public " + collection_type + " LoadFromXmlFile(string filename)");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "string file_data = FileIo.ReadFromFile(filename);");
            sb.AppendLine(AddTabs(4) + "DataSet ds = XmlConverter.XmlStringToDataSet(file_data);");
            sb.AppendLine(AddTabs(4) + "return LoadFromDataTable(ds.Tables[\"" + sqlTable.Name + "\"]);");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region SaveToXmlFile
            ////////////////////////////////////////////////////////////////////////////////

            //public void SaveToXmlFile(List<cGame> collection, string filename)
            //{
            //    DataTable dt = ConvertToDataTable(collection);
            //    string xml = XmlConverter.DataTableToXmlString(dt, "GalacticConquest");
            //    FileIo.WriteToFile(filename, xml);
            //}

            sb.AppendLine(AddTabs(3) + "public void SaveToXmlFile(" + collection_type + " collection, string filename)");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "DataTable dt = ConvertToDataTable(collection);");
            sb.AppendLine(AddTabs(4) + "string xml = XmlConverter.DataTableToXmlString(dt, \"" + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name) + "\");");
            sb.AppendLine(AddTabs(4) + "FileIo.WriteToFile(filename, xml);");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region SaveAll Method
            ////////////////////////////////////////////////////////////////////////////////

            //public void SaveAll(List<cGame> collection)
            //{
            //    foreach (var item in collection)
            //        Save(item);
            //}

            sb.AppendLine(AddTabs(3) + "public void SaveAll(" + collection_type + " collection)");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "foreach (var item in collection)");
            sb.AppendLine(AddTabs(5) + "Save(item);");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region Save Method
            ////////////////////////////////////////////////////////////////////////////////

            //public bool Save(cGame item)
            //{
            //    List<SqlParameter> parameters = GetSqlParameterList(item);
            //    return Database.ExecuteNonQuerySp("[GalacticConquest].[dbo].[up_Game_Set]", parameters, _SQLConnection) > 0;
            //}

            sb.AppendLine(AddTabs(2) + "public bool Save(" + list_type + " item)");
            sb.AppendLine(AddTabs(2) + "{");
            sb.AppendLine(AddTabs(3) + "var parameters = GetSqlParameterList(item);");
            sb.AppendLine(AddTabs(3) + "return Database.ExecuteNonQuerySp(\"" + NameFormatter.ToTSQLName(sqlTable.Database.Name) + ".[dbo].[" + NameFormatter.GenerateSqlStoredProcName(sqlTable.Name, eStoredProcType.UpdateInsert, null) + "]\", parameters, _SQLConnection) > 0;");
            sb.AppendLine(AddTabs(2) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region LoadFromDataTable
            ////////////////////////////////////////////////////////////////////////////////

            //protected List<cGame> LoadFromDataTable(DataTable dt)
            //{
            //    if (dt == null || dt.Rows.Count < 1 || dt.Columns.Count < 1)
            //        return null;

            //    var results = new List<cGame>();

            //    foreach (DataRow dr in dt.Rows)
            //    {
            //        var obj = new cGame();

            //        obj.Id = Convert.ToInt32(dr["Id"]);
            //        obj.Name = Convert.ToString(dr["Name"]);
            //        obj.StarDate = Convert.ToDouble(dr["StarDate"]);
            //        obj.GameStateId = Convert.ToInt32(dr["GameStateId"]);
            //        obj.CreatedById = Convert.ToInt32(dr["CreatedById"]);
            //        obj.CreatedDate = Convert.ToDateTime(dr["CreatedDate"]);
            //        obj.Description = Convert.ToString(dr["Description"]);
            //        obj.Disabled = Convert.ToBoolean(dr["Disabled"]);

            //        results.Add(obj);
            //    }

            //    return results;
            //}

            sb.AppendLine(AddTabs(3) + $"protected {collection_type} LoadFromDataTable(DataTable dt)");
            sb.AppendLine(AddTabs(3) + "{");

            sb.AppendLine(AddTabs(4) + "if (dt == null || dt.Rows.Count < 1 || dt.Columns.Count < 1)");
            sb.AppendLine(AddTabs(5) + "return null;");
            sb.AppendLine();

            sb.AppendLine(AddTabs(4) + "var results = new " + collection_type + "();");
            sb.AppendLine();

            sb.AppendLine(AddTabs(4) + "foreach (DataRow dr in dt.Rows)");
            sb.AppendLine(AddTabs(4) + "{");
            sb.AppendLine(AddTabs(5) + NameFormatter.ToCSharpClassName(sqlTable.Name) + " obj = new " + NameFormatter.ToCSharpClassName(sqlTable.Name) + "();");
            sb.AppendLine();

            foreach (var sql_column in sqlTable.Columns.Values)
            {
                sb.Append(AddTabs(4));

                if (sql_column.IsNullable)
                {
                    // hack - nullable doubles need special treatment
                    switch (sql_column.SqlDataType)
                    {
                        case SqlDbType.Float:

                            sb.Append("obj." + NameFormatter.ToCSharpPropertyName(sql_column.Name));
                            sb.Append("= (dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"] == DBNull.Value) ? null : ");
                            sb.Append("new double?(" + NameFormatter.GetCSharpCastString(sql_column) + "(dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"]));");
                            break;

                        case SqlDbType.UniqueIdentifier:

                            sb.Append("obj." + NameFormatter.ToCSharpPropertyName(sql_column.Name));
                            sb.Append("= (dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"] == DBNull.Value) ? null : ");
                            sb.Append("new Guid?(new Guid((string)dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"]));");
                            break;

                        case SqlDbType.DateTime:

                            sb.Append("obj." + NameFormatter.ToCSharpPropertyName(sql_column.Name));
                            sb.Append("= (dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"] == DBNull.Value) ? null : ");
                            sb.Append("new DateTime?(Convert.ToDateTime(dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"]));");
                            break;

                        // TODO may need more switch cases here...

                        default:

                            sb.Append("obj." + NameFormatter.ToCSharpPropertyName(sql_column.Name));
                            sb.Append("= (dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"] == DBNull.Value) ? null : ");
                            sb.Append(NameFormatter.GetCSharpCastString(sql_column) + "(dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"]);");
                            break;
                    }
                }
                else
                {
                    switch (sql_column.SqlDataType)
                    {
                        case SqlDbType.UniqueIdentifier:

                            sb.Append("obj." + NameFormatter.ToCSharpPropertyName(sql_column.Name));
                            sb.Append("= new Guid((string)(dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"]));");
                            break;

                        default:

                            sb.Append("obj." + NameFormatter.ToCSharpPropertyName(sql_column.Name));
                            sb.Append("= " + NameFormatter.GetCSharpCastString(sql_column) + "(dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"]);");
                            break;
                    }
                }

                sb.AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine(AddTabs(5) + "results.Add(obj);");
            sb.AppendLine(AddTabs(4) + "}");
            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "return results;");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region ConvertToDataTable
            ////////////////////////////////////////////////////////////////////////////////

            //protected DataTable ConvertToDataTable(List<cGame> collection)
            //{
            //    var dt = new DataTable();

            //    dt.Columns.Add("Id", typeof(int));
            //    dt.Columns.Add("Name", typeof(string));
            //    dt.Columns.Add("Disabled", typeof(bool));

            //    dt.TableName = "Game";

            //    foreach (var obj in collection)
            //    {
            //        var dr = dt.NewRow();

            //        dr["Id"]            = obj.Id;
            //        dr["Name"]          = obj.Name;
            //        dr["Disabled"]      = obj.Disabled;

            //        dt.Rows.Add(dr);
            //    }

            //    return dt;
            //}

            sb.AppendLine(AddTabs(3) + "protected DataTable ConvertToDataTable(" + collection_type + " collection)");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "var dt = new DataTable();");
            sb.AppendLine();

            // dt.Columns.Add("Name", typeof(string));
            foreach (var sql_column in sqlTable.Columns.Values)
                sb.AppendLine(AddTabs(4) + "dt.Columns.Add(\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\", typeof(" + NameFormatter.SQLTypeToCSharpType(sql_column) + "));");

            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "dt.TableName = \"" + sqlTable.Name + "\";");
            sb.AppendLine();

            sb.AppendLine(AddTabs(4) + "foreach (var obj in collection)");
            sb.AppendLine(AddTabs(4) + "{");
            sb.AppendLine(AddTabs(5) + "var dr = dt.NewRow();");
            sb.AppendLine();

            foreach (var sql_column in sqlTable.Columns.Values)
            {
                sb.Append(AddTabs(5));
                sb.Append("dr[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"]");
                sb.Append("= obj." + NameFormatter.ToCSharpPropertyName(sql_column.Name) + ";" + Environment.NewLine);
            }

            sb.AppendLine();
            sb.AppendLine(AddTabs(5) + "dt.Rows.Add(dr);");
            sb.AppendLine(AddTabs(4) + "}");
            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "return dt;");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region GetSqlParameterList Method
            ////////////////////////////////////////////////////////////////////////////////

            //protected SqlParameter[] GetSqlParameterList(cRace obj)
            //{
            //    return new SqlParameter[]
            //    {
            //          new SqlParameter() { ParameterName = "AccountId", SqlDbType = SqlDbType.Int, Value = accountId },
            //    };
            //}

            sb.AppendLine(AddTabs(2) + "protected SqlParameter[] GetSqlParameterList(" + NameFormatter.ToCSharpClassName(sqlTable.Name) + " obj)");
            sb.AppendLine(AddTabs(2) + "{");
            sb.AppendLine(AddTabs(3) + "return new SqlParameter[]");
            sb.AppendLine(AddTabs(3) + "{");

            // build parameter collection here
            foreach (var sql_column in sqlTable.Columns.Values)
                sb.AppendLine(AddTabs(4) + NameFormatter.ToCSharpSQLParameterTypeString(sql_column, convertNullableFields));

            sb.AppendLine(AddTabs(3) + "};");
            sb.AppendLine(AddTabs(2) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region ListLoader Method
            ////////////////////////////////////////////////////////////////////////////////

            //public List<string> Load(SqlDataReader reader)
            //{
            //    var output = new List<string>();
            //
            //    while (reader.Read())
            //    {
            //        string name = (string)reader["Name"];
            //
            //        output.Add(id, name);
            //    }
            //
            //    return output;
            //}

            string orm_name = NameFormatter.ToCSharpClassName(sqlTable);

            sb.AppendLine(AddTabs(2) + $"public List<{orm_name}> LoadList(SqlDataReader reader)");
            sb.AppendLine(AddTabs(2) + "{");
            sb.AppendLine(AddTabs(3) + $"var output = new List<{orm_name}>();");
            sb.AppendLine();
            sb.AppendLine(AddTabs(3) + "while (reader.Read())");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + $"var buffer = new {orm_name}();");
            sb.AppendLine();

            foreach (var sql_column in sqlTable.Columns.Values)
            {
                string type = NameFormatter.SQLTypeToCSharpType(sql_column);
                string name = NameFormatter.ToCSharpPropertyName(sql_column);

                sb.AppendLine($"{AddTabs(4)}buffer.{name} = ({type})reader[\"{sql_column.Name}\"];");
            }

            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "output.Add(buffer);");

            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();
            sb.AppendLine(AddTabs(3) + "return output;");
            sb.AppendLine(AddTabs(2) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            #region DatabaseLoader Method
            ////////////////////////////////////////////////////////////////////////////////

            //public Dictionary<int, string> Load(SqlDataReader reader)
            //{
            //    var output = new Dictionary<int, string>();
            //
            //    while (reader.Read())
            //    {
            //        int id = (int)reader["Id"];
            //        string name = (string)reader["Name"];
            //
            //        output.Add(id, name);
            //    }
            //
            //    return output;
            //}

            sb.AppendLine(AddTabs(2) + $"public Dictionary<{NameFormatter.SQLTypeToCSharpType(id_column)}, {orm_name}> LoadDictionary(SqlDataReader reader)");
            sb.AppendLine(AddTabs(2) + "{");
            sb.AppendLine(AddTabs(3) + $"var output = new Dictionary<{NameFormatter.SQLTypeToCSharpType(id_column)}, {orm_name}>();");
            sb.AppendLine();
            sb.AppendLine(AddTabs(3) + "while (reader.Read())");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + $"var buffer = new {orm_name}();");
            sb.AppendLine();

            foreach (var sql_column in sqlTable.Columns.Values)
            {
                string type = NameFormatter.SQLTypeToCSharpType(sql_column);
                string name = NameFormatter.ToCSharpPropertyName(sql_column);

                sb.AppendLine($"{AddTabs(4)}buffer.{name} = ({type})reader[\"{sql_column.Name}\"];");
            }

            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "output.Add(buffer.Id, buffer);");

            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();
            sb.AppendLine(AddTabs(3) + "return output;");
            sb.AppendLine(AddTabs(2) + "}");
            sb.AppendLine();

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            sb.AppendLine(AddTabs(1) + "}");
            sb.AppendLine("}");

            output.Body = sb.ToString();

            return output;
        }

        public OutputObject GenerateCSharpExternalDalClass(SqlTable sqlTable, List<string> namespaceIncludes)
        {
            #region SampleCode
            //public partial class FooClass
            //{
            //}
            #endregion

            if (sqlTable == null)
                return null;

            string class_name = NameFormatter.ToCSharpClassName("Dal" + sqlTable.Name);

            OutputObject output = new OutputObject();
            output.Name = class_name + "_Ext.cs";
            output.Type = OutputObject.eObjectType.CSharp;

            var sb = new StringBuilder();

            #region Header block

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            //sb.AppendLine("using System.Configuration;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine("using System.Data.SqlClient;");
            //sb.AppendLine("using System.Linq;");
            //sb.AppendLine("using System.Text;");
            sb.AppendLine();

            sb.AppendLine("using DAL;");
            sb.AppendLine(GenerateNamespaceIncludes(namespaceIncludes));

            #endregion

            sb.AppendLine("namespace " + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name));
            sb.AppendLine("{");
            sb.AppendLine(AddTabs(1) + "public partial class " + class_name);
            sb.AppendLine(AddTabs(1) + "{");
            sb.AppendLine();
            sb.AppendLine(AddTabs(1) + "}");
            sb.AppendLine("}");

            output.Body = sb.ToString();

            return output;
        }

        public OutputObject GenerateCSharpClassInterface(SqlTable sqlTable, List<string> namespaceIncludes, bool includeIsDirtyFlag)
        {
            if (sqlTable == null)
                return null;

            string interface_name = NameFormatter.ToCSharpInterfaceName(sqlTable.Name);

            OutputObject output = new OutputObject();
            output.Name = interface_name + ".cs";
            output.Type = OutputObject.eObjectType.CSharp;

            #region SampleCode
            //public interface ICar
            //{
            //    void StartEngine();
            //    bool IsMoving { get; }
            //}
            #endregion

            var sb = new StringBuilder();

            #region Header block

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Data;");
            sb.AppendLine();

            sb.AppendLine(GenerateNamespaceIncludes(namespaceIncludes));

            #endregion

            sb.AppendLine("namespace " + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name));
            sb.AppendLine("{");

            sb.AppendLine(AddTabs(1) + "public interface " + interface_name);
            sb.AppendLine(AddTabs(1) + "{");

            #region Properties Block
            ////////////////////////////////////////////////////////////////////////////////
            sb.AppendLine(AddTabs(2) + "#region Properties");
            sb.AppendLine();

            foreach (var sql_column in sqlTable.Columns.Values)
            {
                // Sample: string SomeID { get; set; }
                sb.AppendLine(AddTabs(3) + NameFormatter.SQLTypeToCSharpType(sql_column) + " " + NameFormatter.ToCSharpPropertyName(sql_column.Name) + " { get; set; }");
            }

            if (includeIsDirtyFlag)
            {
                sb.AppendLine();
                sb.AppendLine(AddTabs(3) + "bool IsDirty { get; set; }");
            }

            sb.AppendLine();
            sb.AppendLine(AddTabs(2) + "#endregion");

            ////////////////////////////////////////////////////////////////////////////////
            #endregion

            sb.AppendLine(AddTabs(1) + "}");
            sb.AppendLine("}");

            output.Body = sb.ToString();
            return output;
        }

        public OutputObject GenerateCSharpEnumeration(SqlTable sqlTable, string stringColumn, string valueColumn, string connectionString)
        {
            if (sqlTable == null)
                return null;

            string enum_name = NameFormatter.ToCSharpEnumName(sqlTable.Name);

            OutputObject output = new OutputObject();
            output.Name = enum_name + ".cs";
            output.Type = OutputObject.eObjectType.CSharp;

            var sb = new StringBuilder();

            // we have table metadata, get table data
            DataTable data_table = GenerateSqlTableData(sqlTable, connectionString);

            sb.AppendLine("using System;");
            sb.AppendLine();

            sb.AppendLine("namespace " + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name));
            sb.AppendLine("{");
            sb.AppendLine(AddTabs(1) + "public enum " + NameFormatter.ToCSharpEnumName(sqlTable.Name));
            sb.AppendLine(AddTabs(1) + "{");

            // list enumeration
            foreach (DataRow dr in data_table.Rows)
            {
                string value_string = dr[valueColumn].ToString();
                string enum_string = dr[stringColumn].ToString();

                // make sure that enums are legal names.
                foreach (string character in CSHARP_UNDESIREABLES)
                    enum_string = enum_string.Replace(character, string.Empty);

                // no leading numbers, foo!
                if (Char.IsNumber(enum_string[0]))
                    enum_string = "N" + enum_string;

                sb.AppendLine(AddTabs(2) + enum_string + " = " + value_string + ",");
            }

            sb.AppendLine(AddTabs(1) + "}");
            sb.AppendLine("}");

            output.Body = sb.ToString();
            return output;
        }

        public OutputObject GenerateCSharpBaseClass(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("Sql database name cannot be null or empty");

            OutputObject output = new OutputObject();
            output.Name = "BaseClass.cs";
            output.Type = OutputObject.eObjectType.CSharp;

            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine();
            sb.AppendLine("namespace " + databaseName);
            sb.AppendLine("{");

            sb.AppendLine(AddTabs(1) + "[Serializable]");
            sb.AppendLine(AddTabs(1) + "public abstract class BaseClass");
            sb.AppendLine(AddTabs(1) + "{");

            sb.AppendLine(AddTabs(3) + "protected bool _IsDirty = false;");
            sb.AppendLine(AddTabs(3) + "protected bool _LoadError = false;");
            sb.AppendLine();

            sb.AppendLine(AddTabs(3) + "public bool IsDirty");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "get { return _IsDirty; }");
            sb.AppendLine(AddTabs(4) + "set { _IsDirty = value; }");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine(AddTabs(3) + "public bool LoadError");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "get { return _LoadError; }");
            sb.AppendLine(AddTabs(4) + "set { _LoadError = value; }");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            sb.AppendLine(AddTabs(3) + "public virtual void Reset()");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "_IsDirty = false;");
            sb.AppendLine(AddTabs(4) + "_LoadError = false;");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            sb.AppendLine(AddTabs(3) + "public override string ToString()");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "var sb = new StringBuilder();");
            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "sb.AppendLine(\"_IsDirty: \" + _IsDirty.ToString());");
            sb.AppendLine(AddTabs(4) + "sb.AppendLine(\"_LoadError: \" + _LoadError.ToString());");
            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "return sb.ToString();");
            sb.AppendLine(AddTabs(3) + "}");

            sb.AppendLine(AddTabs(1) + "}");
            sb.AppendLine("}");

            output.Body = sb.ToString();
            return output;
        }

        public DataTable GenerateSqlTableData(SqlTable sqlTable, string connectionString)
        {
            if (sqlTable == null)
                throw new ArgumentException("SqlTable is null");

            string query = $"SELECT * FROM [{sqlTable.Database.Name}].[{sqlTable.Schema}].[{sqlTable.Name}]";
            var db = new Database(connectionString);

            DataTable data_table = db.ExecuteQuery(query, null);
            data_table.TableName = sqlTable.Name;

            return data_table;
        }

        public OutputObject GenerateXmlLoader(SqlTable sqlTable)
        {
            if (sqlTable == null)
                return null;

            string object_name = NameFormatter.ToCSharpClassName(sqlTable.Name);
            string class_name = object_name + "DataLoader";
            string collection_type = $"List<{object_name}>";

            OutputObject output = new OutputObject();
            output.Name = class_name + ".cs";
            output.Type = OutputObject.eObjectType.CSharp;

            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Xml;");
            sb.AppendLine();
            sb.AppendLine("using DAL;");
            sb.AppendLine();

            sb.AppendLine("namespace " + NameFormatter.ToCSharpPropertyName(sqlTable.Database.Name));
            sb.AppendLine("{");
            sb.AppendLine(AddTabs(1) + "[Serializable]");
            sb.AppendLine(AddTabs(1) + "public partial class " + class_name);
            sb.AppendLine(AddTabs(1) + "{");

            sb.AppendLine(AddTabs(2) + "#region Fields");
            sb.AppendLine();
            sb.AppendLine(AddTabs(3) + "protected bool _ParseErrors;");
            sb.AppendLine(AddTabs(3) + "protected " + collection_type + " _CollectionData;");
            sb.AppendLine();
            sb.AppendLine(AddTabs(2) + "#endregion");
            sb.AppendLine();

            sb.AppendLine(AddTabs(2) + "#region Properties");
            sb.AppendLine();
            sb.AppendLine(AddTabs(3) + "public " + collection_type + " CollectionData");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "get { return _CollectionData; }");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();
            sb.AppendLine(AddTabs(2) + "#endregion");
            sb.AppendLine();

            sb.AppendLine(AddTabs(2) + "#region Methods");
            sb.AppendLine();

            sb.AppendLine(AddTabs(3) + "//CTOR");
            sb.AppendLine(AddTabs(3) + "public " + class_name + "()");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(3) + "}");
            sb.AppendLine();

            sb.AppendLine(AddTabs(3) + "public bool ParseData(string file_name)");
            sb.AppendLine(AddTabs(3) + "{");
            sb.AppendLine(AddTabs(4) + "string xml;");
            sb.AppendLine(AddTabs(4) + "string error_message;");
            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "_ParseErrors = !FileIo.ReadFromFile(file_name, out xml, out error_message);");
            sb.AppendLine(AddTabs(4) + "_CollectionData = new " + collection_type + "();");
            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "XmlDocument document;");
            sb.AppendLine(AddTabs(4) + "XmlNodeList selected_nodes;");
            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + object_name + " item_data;");
            sb.AppendLine();
            sb.AppendLine(AddTabs(4) + "try");
            sb.AppendLine(AddTabs(4) + "{");
            sb.AppendLine(AddTabs(5) + "document = new XmlDocument();");
            sb.AppendLine(AddTabs(5) + "document.InnerXml = xml;");
            sb.AppendLine();
            sb.AppendLine(AddTabs(5) + "selected_nodes = document.SelectNodes(\"//" + NameFormatter.ToCSharpPropertyName(sqlTable.Name) + "\");");
            sb.AppendLine();
            sb.AppendLine(AddTabs(5) + "foreach (XmlNode item in selected_nodes)");
            sb.AppendLine(AddTabs(5) + "{");
            sb.AppendLine(AddTabs(6) + "item_data = new " + object_name + "();");
            sb.AppendLine();

            foreach (var sql_column in sqlTable.Columns.Values)
            {
                // format: 
                // critter_data.Name = item.Attributes["Name"].Value;
                sb.AppendLine(AddTabs(6) + "item_data." + NameFormatter.ToCSharpPropertyName(sql_column.Name) + " = " + NameFormatter.GetCSharpCastString(sql_column) + "(item.Attributes[\"" + NameFormatter.ToCSharpPropertyName(sql_column.Name) + "\"].Value);");
            }

            sb.AppendLine();
            sb.AppendLine(AddTabs(6) + "_CollectionData.Add(item_data);");
            sb.AppendLine(AddTabs(5) + "}");
            sb.AppendLine(AddTabs(4) + "}");
            sb.AppendLine(AddTabs(4) + "catch");
            sb.AppendLine(AddTabs(4) + "{");
            sb.AppendLine(AddTabs(5) + "_ParseErrors = true;");
            sb.AppendLine(AddTabs(4) + "}");
            sb.AppendLine(AddTabs(4) + "finally");
            sb.AppendLine(AddTabs(4) + "{");
            sb.AppendLine(AddTabs(5) + "document = null;");
            sb.AppendLine(AddTabs(5) + "selected_nodes = null;");
            sb.AppendLine(AddTabs(4) + "}");
            sb.AppendLine();

            sb.AppendLine(AddTabs(3) + "return !_ParseErrors;");
            sb.AppendLine(AddTabs(2) + "}");
            sb.AppendLine();
            sb.AppendLine(AddTabs(2) + "#endregion");
            sb.AppendLine(AddTabs(1) + "}");

            sb.AppendLine("}");

            output.Body = sb.ToString();
            return output;
        }
    }
}
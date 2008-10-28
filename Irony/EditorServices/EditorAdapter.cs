﻿#region License
/* **********************************************************************************
 * Copyright (c) Roman Ivantsov
 * This source code is subject to terms and conditions of the MIT License
 * for Irony. A copy of the license can be found in the License.txt file
 * at the root of this distribution. 
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the 
 * MIT License.
 * You must not remove this notice from this software.
 * **********************************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Irony.Compiler;

namespace Irony.EditorServices {

  public class ParsedSource {
    public readonly string Text;
    public readonly TokenList Tokens;
    public AstNode Root;
    internal ParsedSource(string text, TokenList tokens, AstNode root) {
      Text = text;
      Tokens = tokens;
      Root = root;
    }

  }//class


  public class EditorAdapter {
    CompilerContext _context;
    LanguageCompiler _compiler;
    ParsedSource _parsedSource;
    string _newText;
    EditorViewAdapterList _views = new EditorViewAdapterList();
    EditorViewAdapterList _viewsCopy; //copy used in refresh loop; set to null when views are added/removed
    Thread _parserThread;
    Thread _colorizerThread;
    bool _stopped;

    public EditorAdapter(LanguageCompiler compiler) {
      _compiler = compiler;
      _context = new CompilerContext(_compiler); 
      _context.Options |= CompilerOptions.CollectTokens;
      _parsedSource = new ParsedSource(string.Empty, new TokenList(), null);
      _colorizerThread = new Thread(ColorizerLoop);
      _colorizerThread.IsBackground = true;
      _parserThread = new Thread(ParserLoop);
      _parserThread.IsBackground = true;
    }
    public void Activate() {
      if ((_colorizerThread.ThreadState & ThreadState.Running) == 0) {
        _parserThread.Start();
        _colorizerThread.Start();
      }
    }

    public void Stop() {
      _stopped = true;
      _parserThread.Join(500);
      if (_parserThread.IsAlive)
        _parserThread.Abort();
      _colorizerThread.Join(500);
      if (_colorizerThread.IsAlive)
        _colorizerThread.Abort();
    }

    public void SetNewText(string text) {
      _newText = text;
    }

    public ParsedSource ParsedSource {
      get { return _parsedSource; }
    }

    //Note: we don't actually parse in current version, only scan. Will implement full parsing in the future, 
    // to support all intellisense operations
    private  void ParseSource(string newText) {
      SourceFile srcFile = new SourceFile(newText, "source");
      _compiler.Scanner.Prepare(_context, srcFile);
      IEnumerable<Token> tokenStream = _compiler.Scanner.BeginScan();
      TokenList newTokens = new TokenList();
      newTokens.AddRange(tokenStream);
      //finally create new contents object and replace the existing _contents value
      _parsedSource = new ParsedSource(newText, newTokens, null);
      //notify views
      var views = GetViews();
      foreach (var view in views)
        view.UpdateParsedSource(_parsedSource);
    }


    #region Views manipulation: AddView, RemoveView, GetViews
    public void AddView(EditorViewAdapter view) {
      lock (this) {
        _views.Add(view);
        _viewsCopy = null;
      }
    }
    public void RemoveView(EditorViewAdapter view) {
      lock (this) {
        _views.Remove(view);
        _viewsCopy = null; 
      }
    }
    private EditorViewAdapterList GetViews() {
      EditorViewAdapterList result = _viewsCopy;
      if (result == null) {
        lock (this) {
          _viewsCopy = new EditorViewAdapterList();
          _viewsCopy.AddRange(_views);
          result = _viewsCopy;
        }//lock
      }
      return result;
    }
    #endregion

    private void ParserLoop() {
      while (!_stopped) {
        ParsedSource source = _parsedSource; 
        string newtext = Interlocked.Exchange(ref _newText, null);
        if (newtext == null || ( source != null && newtext == source.Text))
          Thread.Sleep(20);
        else {
          ParseSource(newtext);
        }
      }//while
    }

    private void ColorizerLoop() {
      while (!_stopped) {
        EditorViewAdapterList views = GetViews();
        //Go through views and invoke refresh
        bool found;
        bool allDone = true;
        do {
          found = false;
          foreach (EditorViewAdapter view in views) {
            if (_stopped) break;
            if (view.WantsColorize) {
              found = true;
              allDone = false;
              view.TryInvokeColorize();
            }
          }//foreach
        } while (found);
        if (allDone)
          Thread.Sleep(100);
      }// while !_stopped
    }//method


  }//class
}//namespace

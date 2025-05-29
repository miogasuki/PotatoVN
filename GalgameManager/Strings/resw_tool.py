#!/usr/bin/env python3
import os
import sys
import argparse
import xml.etree.ElementTree as ET
import fnmatch
from pathlib import Path

class ResourceManager:
    def __init__(self, base_dir="."):
        self.base_dir = Path(base_dir)
        self.language_files = self._find_resource_files()
    
    def _find_resource_files(self):
        """查找所有语言目录下的Resources.resw文件"""
        files = {}
        for item in self.base_dir.iterdir():
            if item.is_dir():
                resw_file = item / "Resources.resw"
                if resw_file.exists():
                    files[item.name] = resw_file
        return files
    
    def _parse_resw_file(self, file_path):
        """解析.resw文件，返回key-value字典"""
        try:
            tree = ET.parse(file_path)
            root = tree.getroot()
            data = {}
            
            for data_elem in root.findall('.//data'):
                name = data_elem.get('name')
                value_elem = data_elem.find('value')
                if name and value_elem is not None:
                    data[name] = value_elem.text or ""
            
            return data
        except ET.ParseError as e:
            print(f"解析文件 {file_path} 时出错: {e}")
            return {}
    
    def search_keys(self, pattern):
        """搜索匹配模式的keys，支持通配符"""
        results = {}
        
        for lang, file_path in self.language_files.items():
            data = self._parse_resw_file(file_path)
            matching_keys = [key for key in data.keys() if fnmatch.fnmatch(key, pattern)]
            
            for key in matching_keys:
                if key not in results:
                    results[key] = {}
                results[key][lang] = data[key]
        
        return results
    
    def update_key(self, key, translations):
        """更新指定key在各语言文件中的值"""
        updated_files = []
        
        for lang, value in translations.items():
            if lang not in self.language_files:
                print(f"警告: 语言 {lang} 的资源文件不存在")
                continue
            
            file_path = self.language_files[lang]
            if self._update_resw_file(file_path, key, value):
                updated_files.append(f"{lang}")
        
        return updated_files
    
    def _update_resw_file(self, file_path, key, value):
        """更新单个.resw文件中的key-value"""
        try:
            tree = ET.parse(file_path)
            root = tree.getroot()
            
            # 查找现有的data元素
            data_elem = None
            for elem in root.findall('.//data'):
                if elem.get('name') == key:
                    data_elem = elem
                    break
            
            # 如果不存在，创建新的data元素
            if data_elem is None:
                data_elem = ET.SubElement(root, 'data')
                data_elem.set('name', key)
                data_elem.set('xml:space', 'preserve')
            
            # 更新或创建value元素
            value_elem = data_elem.find('value')
            if value_elem is None:
                value_elem = ET.SubElement(data_elem, 'value')
            
            value_elem.text = value
            
            # 保存文件
            tree.write(file_path, encoding='utf-8', xml_declaration=True)
            return True
            
        except Exception as e:
            print(f"更新文件 {file_path} 时出错: {e}")
            return False

def main():
    parser = argparse.ArgumentParser(description='Windows资源文件管理工具')
    parser.add_argument('--dir', default='.', help='资源文件根目录 (默认: 当前目录)')
    
    subparsers = parser.add_subparsers(dest='command', help='可用命令')
    
    # 搜索命令
    search_parser = subparsers.add_parser('search', help='搜索资源key')
    search_parser.add_argument('pattern', help='搜索模式 (支持通配符，如: *Login*)')
    
    # 更新命令
    update_parser = subparsers.add_parser('update', help='更新资源key')
    update_parser.add_argument('key', help='要更新的key')
    update_parser.add_argument('translations', nargs='+', 
                              help='语言-值对，格式: lang1=value1 lang2=value2')
    
    args = parser.parse_args()
    
    if not args.command:
        parser.print_help()
        return
    
    rm = ResourceManager(args.dir)
    
    if not rm.language_files:
        print("未找到任何语言资源文件")
        return
    
    print(f"找到语言文件: {', '.join(rm.language_files.keys())}")
    
    if args.command == 'search':
        results = rm.search_keys(args.pattern)
        
        if not results:
            print(f"未找到匹配模式 '{args.pattern}' 的key")
            return
        
        print(f"\n匹配模式 '{args.pattern}' 的结果:")
        print("-" * 80)
        
        for key, translations in results.items():
            print(f"\nKey: {key}")
            for lang, value in translations.items():
                print(f"  {lang}: {value}")
    
    elif args.command == 'update':
        # 解析语言-值对
        translations = {}
        try:
            for item in args.translations:
                if '=' not in item:
                    print(f"错误: 格式不正确 '{item}'，应为 language=value")
                    return
                
                lang, value = item.split('=', 1)
                translations[lang] = value
        except ValueError:
            print("错误: 语言-值对格式不正确")
            return
        
        updated_files = rm.update_key(args.key, translations)
        
        if updated_files:
            print(f"成功更新key '{args.key}' 在以下语言中: {', '.join(updated_files)}")
        else:
            print(f"未能更新任何文件")

if __name__ == '__main__':
    main()

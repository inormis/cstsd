﻿/*
 * This TypeScript definition file contains some core WinRT type 
 * definitions that the TypeScriptD output may need for things like async etc.
 *
 * Include this definition with the rest of your *.d.ts includes.
 *
 */


declare module ToTypeScriptD.WinRT {
    export interface IPromise<TResult> {
        then<U>(success?: (value: TResult) => IPromise<U>, error?: (error: any) => IPromise<U>, progress?: (progress: any) => void): IPromise<U>;
        then<U>(success?: (value: TResult) => IPromise<U>, error?: (error: any) => U, progress?: (progress: any) => void): IPromise<U>;
        then<U>(success?: (value: TResult) => U, error?: (error: any) => IPromise<U>, progress?: (progress: any) => void): IPromise<U>;
        then<U>(success?: (value: TResult) => U, error?: (error: any) => U, progress?: (progress: any) => void): IPromise<U>;
        done<U>(success?: (value: TResult) => any, error?: (error: any) => any, progress?: (progress: any) => void): void;
        cancel(): void;

        //operation: Windows.Foundation.IAsyncOperation<T>;
    }
}
